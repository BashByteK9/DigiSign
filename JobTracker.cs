using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DigiSign
{
    public enum JobStage
    {
        Received,
        Fetching,
        Downloading,
        Signing,
        SkippedSigning,
        Printing,
        Completed,
        Failed
    }

    public class JobRecord
    {
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

        // Insertion order, used for bounded eviction - not for display.
        internal long Sequence { get; set; }
    }

    public static class JobTracker
    {
        private const int MaxHistory = 200;

        private static readonly ConcurrentDictionary<string, JobRecord> jobs = new ConcurrentDictionary<string, JobRecord>();
        private static long sequenceCounter = 0;

        public static JobRecord CreateJob(string token, string route)
        {
            var record = new JobRecord
            {
                JobId = Guid.NewGuid().ToString("N"),
                Token = token,
                Route = route,
                StartedAtUtc = DateTime.UtcNow,
                Stage = JobStage.Received,
                ProgressDetail = "Request received",
                Sequence = System.Threading.Interlocked.Increment(ref sequenceCounter)
            };

            jobs[record.JobId] = record;
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
                }
            }
        }

        public static void SetCallbackResult(string jobId, bool success, string message)
        {
            if (jobs.TryGetValue(jobId, out var record))
            {
                lock (record)
                {
                    record.CallbackSuccess = success;
                    record.CallbackMessage = message;
                }
            }
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
                    Sequence = source.Sequence
                };
            }
        }

        private static void EvictOldest()
        {
            if (jobs.Count <= MaxHistory)
                return;

            var oldest = jobs.Values
                .OrderBy(r => r.Sequence)
                .Take(jobs.Count - MaxHistory)
                .Select(r => r.JobId)
                .ToList();

            foreach (var id in oldest)
                jobs.TryRemove(id, out _);
        }
    }
}
