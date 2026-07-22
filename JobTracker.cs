using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;

namespace DigiSign
{
    public enum JobStage
    {
        Received,
        Fetching,
        Signing,
        Signed,
        SkippedSigning,
        Printing,
        Printed,
        Completed,
        Failed,
        Cancelled,
        Interrupted
    }

    public enum JobSource
    {
        Listener,
        Batch,
        LabelPrint
    }

    public enum ResumeOutcome
    {
        NotFound,
        NotResumable,
        AlreadyRunning,
        Started
    }

    public class JobRecord
    {
        private static readonly HashSet<JobStage> TerminalStages = new HashSet<JobStage>
        {
            JobStage.Completed, JobStage.Failed, JobStage.Cancelled, JobStage.Interrupted
        };

        public string JobId { get; set; }
        public string Token { get; set; }
        public string Route { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public JobStage Stage { get; set; }
        public string DocumentType { get; set; }
        public string FileName { get; set; }
        public string ProgressDetail { get; set; }
        public bool? Success { get; set; }
        public string ErrorMessage { get; set; }
        public string OutputPath { get; set; }
        public bool? CallbackSuccess { get; set; }
        public string CallbackMessage { get; set; }

        // Fields needed to genuinely resume a job (redo fetch/callback) or attribute it, added
        // alongside disk persistence - none of this existed when jobs were purely in-memory.
        public string ClientId { get; set; }
        public string InvoiceNo { get; set; }
        public JobSource Source { get; set; }
        public string InputPath { get; set; }
        public List<string> PathsToPrint { get; set; } = new List<string>();
        public string PrinterName { get; set; }
        public bool DoSign { get; set; }
        public bool DoPrint { get; set; }
        public bool CancellationRequested { get; set; }
        public DateTime? CancelRequestedAtUtc { get; set; }
        public int? OwnerProcessId { get; set; }
        public DateTime? OwnerProcessStartTimeUtc { get; set; }
        public int ResumeCount { get; set; }

        // Insertion order, used for bounded in-memory eviction - not for display.
        internal long Sequence { get; set; }

        /// <summary>True if this job is stopped in a state a user can meaningfully retry from.</summary>
        [JsonIgnore]
        public bool IsResumable =>
            Stage == JobStage.Failed || Stage == JobStage.Interrupted || Stage == JobStage.Cancelled ||
            (Stage == JobStage.Completed && CallbackSuccess == false);

        /// <summary>True if this job is still actively progressing and a cooperative cancel could still take effect before its next step.</summary>
        [JsonIgnore]
        public bool IsCancelable => !CancellationRequested && !TerminalStages.Contains(Stage);

        /// <summary>True if a resume for this job is currently running on a ThreadPool thread right now.
        /// Not persisted - computed at snapshot time from JobTracker's activeJobIds set. Lets the UI show
        /// the job is in flight even while Stage/Success still reflect its previous terminal outcome.</summary>
        [JsonIgnore]
        public bool IsActive { get; set; }
    }

    public static class JobTracker
    {
        private const int MaxHistory = 200;

        // Only these stages are eligible for in-memory eviction once MaxHistory is exceeded -
        // Interrupted and every actively-progressing stage are never evicted, regardless of age,
        // since they represent work a user might still need to see and act on (Resume/Cancel).
        // Eviction here only bounds what's held in memory/shown live - it never deletes the
        // on-disk checkpoint file (that's JobStore.Prune's separate, time-based job).
        private static readonly HashSet<JobStage> EvictableStages = new HashSet<JobStage>
        {
            JobStage.Completed, JobStage.Failed, JobStage.Cancelled
        };

        private static readonly ConcurrentDictionary<string, JobRecord> jobs = new ConcurrentDictionary<string, JobRecord>();
        private static readonly ConcurrentDictionary<string, byte> activeJobIds = new ConcurrentDictionary<string, byte>();
        private static readonly Dictionary<JobSource, Action<string>> resumeHandlers = new Dictionary<JobSource, Action<string>>();
        private static long sequenceCounter = 0;

        public static JobRecord CreateJob(string token, string route, string clientId, string invoiceNo, JobSource source, string inputPath = null, bool doSign = false, bool doPrint = false)
        {
            var record = new JobRecord
            {
                JobId = Guid.NewGuid().ToString("N"),
                Token = token,
                Route = route,
                ClientId = clientId,
                InvoiceNo = invoiceNo,
                Source = source,
                InputPath = inputPath,
                DoSign = doSign,
                DoPrint = doPrint,
                StartedAtUtc = DateTime.UtcNow,
                Stage = JobStage.Received,
                ProgressDetail = "Request received",
                Sequence = Interlocked.Increment(ref sequenceCounter)
            };

            jobs[record.JobId] = record;
            JobStore.Save(record);
            EvictOldest();
            return record;
        }

        public static void UpdateStage(string jobId, JobStage stage, string detail = null)
        {
            if (jobs.TryGetValue(jobId, out var record))
            {
                lock (record)
                {
                    record.Stage = stage;
                    if (detail != null)
                        record.ProgressDetail = detail;
                    JobStore.Save(record);
                }
            }
        }

        public static void UpdateDetail(string jobId, string detail)
        {
            if (jobs.TryGetValue(jobId, out var record))
            {
                lock (record)
                {
                    record.ProgressDetail = detail;
                    JobStore.Save(record);
                }
            }
        }

        public static void SetDocumentInfo(string jobId, string documentType, string fileName)
        {
            if (jobs.TryGetValue(jobId, out var record))
            {
                lock (record)
                {
                    record.DocumentType = documentType;
                    record.FileName = fileName;
                    JobStore.Save(record);
                }
            }
        }

        /// <summary>Checkpoints that signing (or a copy-without-signing) has completed, with the resolved output path(s) - the resume fast-path trusts this.</summary>
        public static void SetSigned(string jobId, bool wasSigned, List<string> pathsToPrint, string printerName)
        {
            if (jobs.TryGetValue(jobId, out var record))
            {
                lock (record)
                {
                    record.Stage = wasSigned ? JobStage.Signed : JobStage.SkippedSigning;
                    record.PathsToPrint = pathsToPrint ?? new List<string>();
                    record.PrinterName = printerName;
                    record.ProgressDetail = wasSigned ? "Signed" : "Copied (not signed)";
                    JobStore.Save(record);
                }
            }
        }

        /// <summary>Checkpoints that printing has completed.</summary>
        public static void SetPrinted(string jobId)
        {
            if (jobs.TryGetValue(jobId, out var record))
            {
                lock (record)
                {
                    record.Stage = JobStage.Printed;
                    record.ProgressDetail = "Printed";
                    JobStore.Save(record);
                }
            }
        }

        public static void Complete(string jobId, bool success, string outputPath, string errorMessage)
        {
            if (jobs.TryGetValue(jobId, out var record))
            {
                lock (record)
                {
                    record.Success = success;
                    record.OutputPath = outputPath;
                    record.ErrorMessage = errorMessage;
                    record.Stage = success ? JobStage.Completed : JobStage.Failed;
                    record.ProgressDetail = success ? "Completed" : (errorMessage ?? "Failed");
                    record.CompletedAtUtc = DateTime.UtcNow;
                    JobStore.Save(record);
                }
            }
            activeJobIds.TryRemove(jobId, out _);
        }

        /// <summary>Checkpoints that a job stopped in response to a cooperative cancel request (between steps, nothing in-flight interrupted).</summary>
        public static void Cancel(string jobId)
        {
            if (jobs.TryGetValue(jobId, out var record))
            {
                lock (record)
                {
                    record.Stage = JobStage.Cancelled;
                    record.ProgressDetail = "Cancelled";
                    record.CompletedAtUtc = DateTime.UtcNow;
                    JobStore.Save(record);
                }
            }
            activeJobIds.TryRemove(jobId, out _);
        }

        public static void SetCallbackResult(string jobId, bool success, string message)
        {
            if (jobs.TryGetValue(jobId, out var record))
            {
                lock (record)
                {
                    record.CallbackSuccess = success;
                    record.CallbackMessage = message;
                    JobStore.Save(record);
                }
            }
        }

        /// <summary>Records which process currently owns (is actively processing) a job - used by startup recovery to tell a crash apart from a still-running sibling process.</summary>
        public static void SetOwner(string jobId, int processId, DateTime processStartTimeUtc)
        {
            if (jobs.TryGetValue(jobId, out var record))
            {
                lock (record)
                {
                    record.OwnerProcessId = processId;
                    record.OwnerProcessStartTimeUtc = processStartTimeUtc;
                    JobStore.Save(record);
                }
            }
        }

        public static JobRecord GetJob(string jobId)
        {
            return jobs.TryGetValue(jobId, out var record) ? CloneRecord(record) : null;
        }

        /// <summary>Requests a cooperative cancel - takes effect the next time the job's pipeline checks between steps; never interrupts a step already in flight.</summary>
        public static void RequestCancel(string jobId)
        {
            if (!jobs.TryGetValue(jobId, out var record))
                return;

            lock (record)
            {
                if (!record.IsCancelable)
                    return;

                record.CancellationRequested = true;
                record.CancelRequestedAtUtc = DateTime.UtcNow;
                record.ProgressDetail = "Cancel requested...";
                JobStore.Save(record);
            }
        }

        /// <summary>Registers the handler invoked (on a ThreadPool thread) when a job of the given source is resumed. Keeps JobTracker decoupled from listener/batch pipeline specifics.</summary>
        public static void RegisterResumeHandler(JobSource source, Action<string> handler)
        {
            resumeHandlers[source] = handler;
        }

        public static ResumeOutcome ResumeJob(string jobId)
        {
            if (!jobs.TryGetValue(jobId, out var record))
                return ResumeOutcome.NotFound;

            lock (record)
            {
                if (!record.IsResumable)
                    return ResumeOutcome.NotResumable;

                if (!activeJobIds.TryAdd(jobId, 0))
                    return ResumeOutcome.AlreadyRunning;

                record.CancellationRequested = false;
                record.CancelRequestedAtUtc = null;
                record.ResumeCount++;
                record.ProgressDetail = "Resuming...";
                JobStore.Save(record);
            }

            if (!resumeHandlers.TryGetValue(record.Source, out var handler))
            {
                Logger.Warning($"No resume handler registered for job source {record.Source} (job {jobId})");
                activeJobIds.TryRemove(jobId, out _);
                return ResumeOutcome.NotResumable;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    handler(jobId);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Unhandled exception resuming job {jobId}", ex);
                    Complete(jobId, false, null, $"Unhandled error during resume: {ex.Message}");
                }
                finally
                {
                    activeJobIds.TryRemove(jobId, out _);
                }
            });

            return ResumeOutcome.Started;
        }

        /// <summary>Loads a job discovered on disk (e.g. by startup recovery) into this process's in-memory snapshot, without re-persisting it.</summary>
        internal static void LoadRecord(JobRecord record)
        {
            if (record.Sequence == 0)
                record.Sequence = Interlocked.Increment(ref sequenceCounter);
            jobs[record.JobId] = record;
        }

        public static List<JobRecord> Snapshot()
        {
            return jobs.Values
                .Select(CloneRecord)
                .OrderByDescending(r => r.Sequence)
                .ToList();
        }

        private static JobRecord CloneRecord(JobRecord source)
        {
            lock (source)
            {
                return new JobRecord
                {
                    JobId = source.JobId,
                    Token = source.Token,
                    Route = source.Route,
                    StartedAtUtc = source.StartedAtUtc,
                    CompletedAtUtc = source.CompletedAtUtc,
                    Stage = source.Stage,
                    DocumentType = source.DocumentType,
                    FileName = source.FileName,
                    ProgressDetail = source.ProgressDetail,
                    Success = source.Success,
                    ErrorMessage = source.ErrorMessage,
                    OutputPath = source.OutputPath,
                    CallbackSuccess = source.CallbackSuccess,
                    CallbackMessage = source.CallbackMessage,
                    ClientId = source.ClientId,
                    InvoiceNo = source.InvoiceNo,
                    Source = source.Source,
                    InputPath = source.InputPath,
                    PathsToPrint = new List<string>(source.PathsToPrint ?? new List<string>()),
                    PrinterName = source.PrinterName,
                    DoSign = source.DoSign,
                    DoPrint = source.DoPrint,
                    CancellationRequested = source.CancellationRequested,
                    CancelRequestedAtUtc = source.CancelRequestedAtUtc,
                    OwnerProcessId = source.OwnerProcessId,
                    OwnerProcessStartTimeUtc = source.OwnerProcessStartTimeUtc,
                    ResumeCount = source.ResumeCount,
                    Sequence = source.Sequence,
                    IsActive = activeJobIds.ContainsKey(source.JobId)
                };
            }
        }

        private static void EvictOldest()
        {
            if (jobs.Count <= MaxHistory)
                return;

            var oldest = jobs.Values
                .Where(r => EvictableStages.Contains(r.Stage))
                .OrderBy(r => r.Sequence)
                .Take(jobs.Count - MaxHistory)
                .Select(r => r.JobId)
                .ToList();

            foreach (var id in oldest)
                jobs.TryRemove(id, out _);
        }
    }
}
