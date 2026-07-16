using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace DigiSign
{
    public class FileSignOutcome
    {
        public string FileName { get; set; }
        public bool Success { get; set; }
        public List<string> OutputPaths { get; set; } = new List<string>();
        public string OutputPath => OutputPaths.Count > 0 ? OutputPaths[0] : null;
        public string Error { get; set; }
    }

    public class BatchSignResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public bool CertificateLoaded { get; set; }
        public string CertificateError { get; set; }
        public List<FileSignOutcome> FileResults { get; } = new List<FileSignOutcome>();

        public IEnumerable<string> SuccessfulOutputPaths =>
            FileResults.Where(r => r.Success).SelectMany(r => r.OutputPaths);
    }

    public interface IBatchSignProgress
    {
        void OnStart(int totalFiles);
        void OnCertificateLoaded(X509Certificate2 cert);
        void OnCertificateNotFound(string commonName);
        void OnFileStart(int index, int total, string fileName);
        void OnFileSuccess(int index, int total, string fileName, string outputPath);
        void OnFileFailure(int index, int total, string fileName, Exception ex);
        void OnComplete(BatchSignResult result);
    }

    public class NullBatchSignProgress : IBatchSignProgress
    {
        public void OnStart(int totalFiles) { }
        public void OnCertificateLoaded(X509Certificate2 cert) { }
        public void OnCertificateNotFound(string commonName) { }
        public void OnFileStart(int index, int total, string fileName) { }
        public void OnFileSuccess(int index, int total, string fileName, string outputPath) { }
        public void OnFileFailure(int index, int total, string fileName, Exception ex) { }
        public void OnComplete(BatchSignResult result) { }
    }

    public static class BatchSigner
    {
        public static BatchSignResult SignFiles(
            IEnumerable<string> inputPdfPaths,
            string outputFolderPath,
            string commonName,
            string pin,
            SignatureConfiguration signatureConfig,
            XmlData xmlData,
            IBatchSignProgress progress = null)
        {
            progress = progress ?? new NullBatchSignProgress();
            var result = new BatchSignResult();

            var validFiles = inputPdfPaths
                .Where(f => File.Exists(f) && Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                .ToList();
            progress.OnStart(validFiles.Count);

            Directory.CreateDirectory(outputFolderPath);

            Logger.Info($"Loading certificate: {commonName}");
            var signatureService = new DigitalSignatureService();
            var cert = signatureService.LoadCertificate(commonName, pin, xmlData);
            if (cert == null)
            {
                result.CertificateError = $"Certificate not found: {commonName}";
                progress.OnCertificateNotFound(commonName);
                progress.OnComplete(result);
                return result;
            }

            Logger.Info("Certificate loaded successfully");
            Logger.Debug($"Certificate Subject: {cert.Subject}");
            Logger.Debug($"Certificate Thumbprint: {cert.Thumbprint}");
            Logger.Debug($"Certificate Expiry: {cert.NotAfter:yyyy-MM-dd}");
            result.CertificateLoaded = true;
            progress.OnCertificateLoaded(cert);

            for (int i = 0; i < validFiles.Count; i++)
            {
                string inputPath = validFiles[i];
                string fileName = Path.GetFileName(inputPath);
                string baseName = Path.GetFileNameWithoutExtension(fileName);
                string extension = Path.GetExtension(fileName);
                string primaryOutputPath = Path.Combine(outputFolderPath, fileName);

                Logger.LogSeparator();
                Logger.Info($"Processing PDF: {fileName}");
                progress.OnFileStart(i, validFiles.Count, fileName);

                try
                {
                    var copyLabels = signatureConfig.GetCopyLabelsToSign();
                    var outputPaths = new List<string>();

                    for (int copyIndex = 0; copyIndex < copyLabels.Count; copyIndex++)
                    {
                        string label = copyLabels[copyIndex];
                        string copyOutputPath = copyIndex == 0
                            ? primaryOutputPath
                            : Path.Combine(outputFolderPath, $"{baseName} - {SanitizeForFileName(label)}{extension}");

                        signatureService.SignPdf(inputPath, copyOutputPath, cert, signatureConfig, pin, outputFolderPath, label);
                        outputPaths.Add(copyOutputPath);
                    }

                    result.SuccessCount++;
                    result.FileResults.Add(new FileSignOutcome { FileName = fileName, Success = true, OutputPaths = outputPaths });
                    Logger.Info($"Successfully signed: {fileName} ({outputPaths.Count} copy(ies))");
                    progress.OnFileSuccess(i, validFiles.Count, fileName, outputPaths[0]);
                }
                catch (Exception ex)
                {
                    result.FailCount++;
                    result.FileResults.Add(new FileSignOutcome { FileName = fileName, Success = false, Error = ex.Message });
                    Logger.Error($"Failed to sign: {fileName}", ex);
                    progress.OnFileFailure(i, validFiles.Count, fileName, ex);
                }
            }

            Logger.LogSeparator();
            Logger.Info($"PDF signing completed - Success: {result.SuccessCount}, Failed: {result.FailCount}");
            progress.OnComplete(result);
            return result;
        }

        private static string SanitizeForFileName(string label)
        {
            string sanitized = label;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(c, '_');
            }
            return sanitized;
        }
    }
}
