using System;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace DigiSign
{
    internal class VerboseBatchSignProgress : IBatchSignProgress
    {
        private readonly VerboseProgressForm form;

        public VerboseBatchSignProgress(VerboseProgressForm form)
        {
            this.form = form;
        }

        public void OnStart(int totalFiles)
        {
            // "Filtering PDF files..." (step 6) and "Loading certificate..." (step 7) narration
            // is handled by the caller before invoking BatchSigner - nothing to do here.
        }

        public void OnCertificateLoaded(X509Certificate2 cert)
        {
            form.AppendSuccess("Certificate loaded");
            form.AppendDetail($"Subject: {cert.Subject}");
            form.AppendDetail($"Expiry: {cert.NotAfter:yyyy-MM-dd}");
            form.AppendText("\n", Color.Black);
            form.UpdateProgress(8, "Processing PDF files...");
            form.AppendText("\n", Color.Black);
            Application.DoEvents();
        }

        public void OnCertificateNotFound(string commonName)
        {
            form.AppendError($"Certificate not found: {commonName}");
            Application.DoEvents();
        }

        public void OnFileStart(int index, int total, string fileName)
        {
            form.AppendText($"\n    PDF {index + 1}/{total}: {fileName}\n", Color.FromArgb(0, 102, 204), true);
            Application.DoEvents();
        }

        public void OnFileSuccess(int index, int total, string fileName, string outputPath)
        {
            form.AppendSuccess("SUCCESS");
            form.AppendDetail($"Output: {fileName}");
            form.AppendText("\n", Color.Black);
            Application.DoEvents();
        }

        public void OnFileFailure(int index, int total, string fileName, Exception ex)
        {
            form.AppendError("FAILED");
            form.AppendDetail($"Error: {ex.Message}");
            form.AppendText("\n", Color.Black);
            Application.DoEvents();
        }

        public void OnComplete(BatchSignResult result)
        {
            form.UpdateProgress(9, "Processing complete");
            form.AppendText("\n", Color.Black);
            form.ShowSummary(result.SuccessCount, result.FailCount);
            form.AppendText("\n", Color.Black);
            Application.DoEvents();
        }
    }
}
