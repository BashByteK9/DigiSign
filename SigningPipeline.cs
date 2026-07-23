using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace DigiSign
{
    public class SignSingleFileResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public List<string> OutputPaths { get; set; } = new List<string>();
        public bool PrintAllCopies { get; set; }
    }

    /// <summary>
    /// Shared "sign exactly one file" orchestration (license check + SignatureConfiguration build +
    /// signingLock-guarded BatchSigner.SignFiles call + result interpretation) - reused by the listener's
    /// per-token pipeline and by batch-mode's per-file pipeline (including batch-mode job resume), so this
    /// isn't triplicated across call sites.
    /// </summary>
    internal static class SigningPipeline
    {
        // Hardware USB-token signing (PIN-protected private key) must not be hit concurrently, across
        // listener requests AND batch-signing runs alike - this lock is shared by every caller rather
        // than duplicated per call site.
        private static readonly SemaphoreSlim signingLock = new SemaphoreSlim(1, 1);

        public static SignSingleFileResult SignSingleFile(
            string inputPath, XmlData xmlData, bool enableOcspCheck, int ocspTimeoutSeconds,
            string licensePath, out string licenseIssue, IBatchSignProgress progress = null)
        {
            licenseIssue = null;

            if (xmlData == null || string.IsNullOrEmpty(xmlData.CommonName) || string.IsNullOrEmpty(xmlData.OutputFolderPath))
            {
                return new SignSingleFileResult
                {
                    Success = false,
                    Error = "PDF signing settings are not configured. Run 'DigiSign.exe /settings' first."
                };
            }

            if (!LicenseManager.ValidateLicense(licensePath))
            {
                // No valid purchased license - fall back to the 30-day evaluation period (if still
                // active) rather than refusing outright. The trial marker lives alongside license.txt.
                string trialPath = Path.Combine(
                    Path.GetDirectoryName(licensePath) ?? System.AppDomain.CurrentDomain.BaseDirectory,
                    TrialManager.TrialFileName);
                var trialStatus = TrialManager.GetTrialStatus(trialPath);

                if (trialStatus.IsActive)
                {
                    Logger.Info($"No valid purchased license - signing in TRIAL MODE ({trialStatus.DaysRemaining} day(s) remaining)");
                }
                else
                {
                    licenseIssue = "Valid license required for signing.";
                    return new SignSingleFileResult { Success = false, Error = licenseIssue };
                }
            }

            var signatureConfig = new SignatureConfiguration(
                xmlData.XCoordinate, xmlData.YCoordinate, xmlData.Width, xmlData.Height)
            {
                Copy1Label = xmlData.Copy1Label,
                ExtraCopiesEnabled = xmlData.ExtraCopiesEnabled,
                PrintAllCopies = xmlData.PrintAllCopies,
                Copy2Label = xmlData.Copy2Label,
                Copy3Label = xmlData.Copy3Label,
                Copy4Label = xmlData.Copy4Label,
                CopyLabelX = xmlData.CopyLabelX,
                CopyLabelY = xmlData.CopyLabelY,
                CopyLabelWidth = xmlData.CopyLabelWidth,
                CopyLabelHeight = xmlData.CopyLabelHeight,
                EnableOcspCheck = enableOcspCheck,
                OcspTimeoutSeconds = ocspTimeoutSeconds
            };

            BatchSignResult result;
            signingLock.Wait();
            try
            {
                result = BatchSigner.SignFiles(new[] { inputPath }, xmlData.OutputFolderPath, xmlData.CommonName, xmlData.Pin, signatureConfig, xmlData, progress);
            }
            finally
            {
                signingLock.Release();
            }

            if (result.CertificateError != null)
                return new SignSingleFileResult { Success = false, Error = result.CertificateError };

            var fileResult = result.FileResults.Count > 0 ? result.FileResults[0] : null;
            if (fileResult == null || !fileResult.Success)
                return new SignSingleFileResult { Success = false, Error = fileResult?.Error ?? "Unknown signing failure" };

            return new SignSingleFileResult
            {
                Success = true,
                OutputPaths = fileResult.OutputPaths,
                PrintAllCopies = signatureConfig.PrintAllCopies
            };
        }
    }
}
