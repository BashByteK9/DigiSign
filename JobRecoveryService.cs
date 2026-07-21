using System;
using System.Diagnostics;

namespace DigiSign
{
    /// <summary>
    /// Runs at startup in every long-lived-ish entry point (listener, tray companion, batch-signing)
    /// to find jobs left non-terminal by a process that crashed or lost power mid-job. A job whose
    /// owning process is confirmed still alive (e.g. a sibling batch CLI invocation still running) is
    /// left alone; everything else is marked Interrupted so a human can Resume or Cancel it - never
    /// auto-resumed silently.
    /// </summary>
    internal static class JobRecoveryService
    {
        private const int OwnerStartTimeToleranceMs = 2000;

        public static void RunStartupRecovery()
        {
            var records = JobStore.LoadAll();
            int interruptedCount = 0;

            foreach (var record in records)
            {
                bool isTerminal = record.Stage == JobStage.Completed || record.Stage == JobStage.Failed
                    || record.Stage == JobStage.Cancelled || record.Stage == JobStage.Interrupted;

                if (isTerminal)
                {
                    JobTracker.LoadRecord(record);
                    continue;
                }

                if (IsOwnerStillAlive(record))
                {
                    Logger.Debug($"Job {record.JobId} still owned by live process {record.OwnerProcessId} - leaving alone");
                    JobTracker.LoadRecord(record);
                    continue;
                }

                JobStage originalStage = record.Stage;
                record.Stage = JobStage.Interrupted;
                record.ProgressDetail = "Interrupted - the process handling this job stopped unexpectedly (crash or power loss). Use Resume to continue, or Cancel to discard.";
                JobStore.Save(record);
                JobTracker.LoadRecord(record);
                interruptedCount++;

                Logger.Warning($"Job {record.JobId} (token={record.Token}, source={record.Source}) was left in stage '{originalStage}' by a process that's no longer running - marked Interrupted");
            }

            if (interruptedCount > 0)
                Logger.Info($"Startup recovery: found {interruptedCount} interrupted job(s) from a previous run");
        }

        private static bool IsOwnerStillAlive(JobRecord record)
        {
            if (record.OwnerProcessId == null || record.OwnerProcessStartTimeUtc == null)
                return false;

            try
            {
                var proc = Process.GetProcessById(record.OwnerProcessId.Value);
                // Guard against PID reuse: the live process must be the same instance that set this
                // ownership, not a different, later process that happened to reuse the same PID.
                return Math.Abs((proc.StartTime.ToUniversalTime() - record.OwnerProcessStartTimeUtc.Value).TotalMilliseconds)
                    < OwnerStartTimeToleranceMs;
            }
            catch (ArgumentException)
            {
                // No process with that Id is currently running.
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning($"JobRecoveryService: could not check owner process liveness for job {record.JobId}: {ex.Message}");
                return false; // fail-safe: treat as interrupted rather than silently leaving a dead job hidden
            }
        }
    }
}
