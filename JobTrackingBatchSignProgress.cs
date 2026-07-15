using System;
using System.Security.Cryptography.X509Certificates;

namespace DigiSign
{
    internal class JobTrackingBatchSignProgress : IBatchSignProgress
    {
        private readonly string jobId;

        public JobTrackingBatchSignProgress(string jobId)
        {
            this.jobId = jobId;
        }

        public void OnStart(int totalFiles)
        {
            JobTracker.UpdateStage(jobId, JobStage.Signing, "Loading certificate...");
        }

        public void OnCertificateLoaded(X509Certificate2 cert)
        {
            JobTracker.UpdateDetail(jobId, "Certificate loaded, signing PDF...");
        }

        public void OnCertificateNotFound(string commonName)
        {
            JobTracker.UpdateDetail(jobId, $"Certificate not found: {commonName}");
        }

        public void OnFileStart(int index, int total, string fileName)
        {
            JobTracker.UpdateDetail(jobId, $"Signing {fileName}...");
        }

        public void OnFileSuccess(int index, int total, string fileName, string outputPath)
        {
            JobTracker.UpdateDetail(jobId, $"Signed {fileName}");
        }

        public void OnFileFailure(int index, int total, string fileName, Exception ex)
        {
            JobTracker.UpdateDetail(jobId, $"Failed to sign {fileName}: {ex.Message}");
        }

        public void OnComplete(BatchSignResult result)
        {
            // Final success/failure/outputPath is decided by the caller (HttpListenerService's
            // pipeline) after inspecting `result`, not here - keeps one source of truth for both
            // the job record and the JSON response.
        }
    }
}
