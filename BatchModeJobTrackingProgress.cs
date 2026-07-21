using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace DigiSign
{
    /// <summary>
    /// Mirrors JobTrackingBatchSignProgress for batch-signing mode, where BatchSigner.SignFiles processes
    /// many files at once - maps each per-index callback back to that file's own job (jobIds[index]).
    /// Final success/failure/outputPath is decided by the caller (Program.cs) after inspecting the
    /// BatchSignResult, not here - same one-source-of-truth split as the listener's equivalent.
    /// </summary>
    internal class BatchModeJobTrackingProgress : IBatchSignProgress
    {
        private readonly IReadOnlyList<string> jobIds;

        public BatchModeJobTrackingProgress(IReadOnlyList<string> jobIds)
        {
            this.jobIds = jobIds;
        }

        public void OnStart(int totalFiles) { }
        public void OnCertificateLoaded(X509Certificate2 cert) { }
        public void OnCertificateNotFound(string commonName) { }

        public void OnFileStart(int index, int total, string fileName)
        {
            if (index >= 0 && index < jobIds.Count)
                JobTracker.UpdateStage(jobIds[index], JobStage.Signing, $"Signing {fileName}...");
        }

        public void OnFileSuccess(int index, int total, string fileName, string outputPath)
        {
            if (index < 0 || index >= jobIds.Count)
                return;

            // Checkpoint immediately (not just at the end of the whole batch) so a crash partway
            // through a multi-file batch still leaves already-signed files resumable without redoing
            // hardware-token signing for them.
            JobTracker.SetSigned(jobIds[index], wasSigned: true, new List<string> { outputPath }, printerName: null);
            JobTracker.UpdateDetail(jobIds[index], $"Signed {fileName}");
        }

        public void OnFileFailure(int index, int total, string fileName, Exception ex)
        {
            if (index >= 0 && index < jobIds.Count)
                JobTracker.UpdateDetail(jobIds[index], $"Failed to sign {fileName}: {ex.Message}");
        }

        public void OnComplete(BatchSignResult result) { }
    }
}
