using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;

namespace DigiSign
{
    public class HttpListenerService : IDisposable
    {
        private static readonly Regex RoutePattern = new Regex(@"^/(invoice|invoice-print|invoice-sign|invoice-sign-print)/([^/]+)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly int port;
        private readonly XmlData xmlData;
        private readonly IDocumentDownloader downloader;
        private readonly string licensePath;
        private readonly Action<string> onLicenseIssue;
        private readonly IPrintService printService;
        private readonly string printerName;

        private HttpListener listener;
        private Thread listenThread;
        private volatile bool stopping;

        public HttpListenerService(int port, XmlData xmlData, IDocumentDownloader downloader, string licensePath, Action<string> onLicenseIssue,
            IPrintService printService, string printerName)
        {
            this.port = port;
            this.xmlData = xmlData;
            this.downloader = downloader;
            this.licensePath = licensePath;
            this.onLicenseIssue = onLicenseIssue;
            this.printService = printService;
            this.printerName = printerName;
        }

        public void Start()
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            stopping = false;
            listenThread = new Thread(ListenLoop) { IsBackground = true };
            listenThread.Start();
        }

        public void Stop()
        {
            stopping = true;
            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch (Exception ex)
            {
                Logger.Error("Error stopping HTTP listener", ex);
            }
            Logger.Info("HTTP listener stopped");
        }

        public void Dispose()
        {
            Stop();
        }

        private void ListenLoop()
        {
            while (!stopping)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (Exception)
                {
                    // Listener stopped/closed - exit the loop
                    break;
                }

                var localContext = context;
                System.Threading.ThreadPool.QueueUserWorkItem(_ => HandleRequestSafe(localContext));
            }
        }

        private void HandleRequestSafe(HttpListenerContext context)
        {
            try
            {
                HandleRequest(context);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception while processing HTTP request", ex);
                try
                {
                    WriteJson(context, 500, new { success = false, error = "Internal server error" });
                }
                catch
                {
                    // Response may already be closed - nothing more we can do
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            string method = context.Request.HttpMethod;
            string path = context.Request.Url.AbsolutePath;
            string remote = context.Request.RemoteEndPoint?.ToString() ?? "unknown";

            Logger.Info($"HTTP request received: {method} {path} from {remote}");

            var match = RoutePattern.Match(path);
            if (!match.Success)
            {
                Logger.Warning($"Unrecognized route requested: {method} {path}");
                WriteJson(context, 404, new
                {
                    success = false,
                    error = "Unknown route. Expected /invoice/{token}, /invoice-print/{token}, /invoice-sign/{token}, or /invoice-sign-print/{token}."
                });
                return;
            }

            string route = match.Groups[1].Value.ToLowerInvariant();
            string token = match.Groups[2].Value;
            if (!IsValidToken(token, out string reason))
            {
                Logger.Warning($"Rejected request with invalid token: {reason}");
                WriteJson(context, 400, new { success = false, token, route, error = reason });
                return;
            }

            bool doSign = route == "invoice-sign" || route == "invoice-sign-print";
            bool doPrint = route == "invoice-print" || route == "invoice-sign-print";

            ProcessDocumentRequest(context, route, token, doSign, doPrint);
        }

        private static bool IsValidToken(string token, out string reason)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                reason = "Token is required.";
                return false;
            }

            if (token.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || token.Contains("..") || token.Contains("/") || token.Contains("\\"))
            {
                reason = "Token contains invalid characters.";
                return false;
            }

            reason = null;
            return true;
        }

        private void ProcessDocumentRequest(HttpListenerContext context, string route, string token, bool doSign, bool doPrint)
        {
            string jobId = JobTracker.CreateJob(token, route).JobId;
            string tokenFolder = Path.Combine(Path.GetTempPath(), $"digisign_{token}_{DateTime.Now:yyyyMMddHHmmssfff}");
            try
            {
                Logger.Info($"[token={token}] Fetching document info");
                JobTracker.UpdateStage(jobId, JobStage.Fetching, "Fetching document info...");
                DocumentInfo info;
                try
                {
                    info = downloader.FetchInfo(token);
                }
                catch (DownloadException ex)
                {
                    Logger.Error($"[token={token}] Info fetch failed", ex);
                    JobTracker.Complete(jobId, false, null, $"Failed to fetch document info: {ex.Message}");
                    WriteJson(context, 502, new { success = false, token, route, error = $"Failed to fetch document info: {ex.Message}" });
                    return;
                }

                if (info == null)
                {
                    Logger.Warning($"[token={token}] No document info returned for token");
                    JobTracker.Complete(jobId, false, null, "No document found for this token.");
                    WriteJson(context, 404, new { success = false, token, route, error = "No document found for this token." });
                    return;
                }

                JobTracker.SetDocumentInfo(jobId, info.DocumentType, info.FileName);

                Directory.CreateDirectory(tokenFolder);

                Logger.Info($"[token={token}] Downloading document ({info.DocumentType}) into {tokenFolder}");
                JobTracker.UpdateStage(jobId, JobStage.Downloading, $"Downloading {info.FileName}...");
                string localFilePath;
                try
                {
                    localFilePath = downloader.Download(info, tokenFolder);
                }
                catch (DownloadException ex)
                {
                    Logger.Error($"[token={token}] Download failed", ex);
                    JobTracker.Complete(jobId, false, null, $"Failed to download document: {ex.Message}");
                    WriteJson(context, 502, new { success = false, token, route, docType = info.DocumentType, error = $"Failed to download document: {ex.Message}" });
                    return;
                }

                // Label documents are never cryptographically signed, regardless of which route was hit.
                bool willSign = doSign && string.Equals(info.DocumentType, "invoice", StringComparison.OrdinalIgnoreCase);

                string finalPath;
                bool signed = false;

                if (willSign)
                {
                    if (!LicenseManager.ValidateLicense(licensePath))
                    {
                        string msg = $"Signing request for token '{token}' was rejected: no valid license is installed. Run 'DigiSign.exe /settings' or contact support for a license.";
                        Logger.Error(msg);
                        JobTracker.Complete(jobId, false, null, "Valid license required for signing.");
                        WriteJson(context, 403, new { success = false, token, route, docType = info.DocumentType, error = "Valid license required for signing." });
                        onLicenseIssue(msg);
                        return;
                    }

                    var signatureConfig = new SignatureConfiguration(
                        xmlData.XCoordinate, xmlData.YCoordinate, xmlData.Width, xmlData.Height, xmlData.SignOnPage);

                    var result = BatchSigner.SignFiles(new[] { localFilePath }, xmlData.OutputFolderPath, xmlData.CommonName, xmlData.Pin, signatureConfig, xmlData,
                        new JobTrackingBatchSignProgress(jobId));

                    if (result.CertificateError != null)
                    {
                        Logger.LogToPlf($"Token {token}: {result.CertificateError}", isError: true);
                        JobTracker.Complete(jobId, false, null, result.CertificateError);
                        WriteJson(context, 500, new { success = false, token, route, docType = info.DocumentType, error = result.CertificateError });
                        return;
                    }

                    var fileResult = result.FileResults.Count > 0 ? result.FileResults[0] : null;
                    if (fileResult == null || !fileResult.Success)
                    {
                        string err = fileResult?.Error ?? "Unknown signing failure";
                        Logger.LogToPlf($"Token {token}: signing failed - {err}", isError: true);
                        JobTracker.Complete(jobId, false, null, err);
                        WriteJson(context, 500, new { success = false, token, route, docType = info.DocumentType, error = err });
                        return;
                    }

                    finalPath = fileResult.OutputPath;
                    signed = true;
                    Logger.LogToPlf($"Token {token}: signed successfully", isError: false);
                }
                else
                {
                    if (doSign)
                        JobTracker.UpdateStage(jobId, JobStage.SkippedSigning, "Label document - signing skipped");

                    Directory.CreateDirectory(xmlData.OutputFolderPath);
                    string destPath = Path.Combine(xmlData.OutputFolderPath, Path.GetFileName(localFilePath));
                    File.Copy(localFilePath, destPath, overwrite: true);
                    finalPath = destPath;
                    Logger.LogToPlf($"Token {token}: fetched (unsigned)", isError: false);
                }

                bool printed = false;
                if (doPrint)
                {
                    JobTracker.UpdateStage(jobId, JobStage.Printing, $"Printing to {(string.IsNullOrWhiteSpace(printerName) ? "default printer" : printerName)}...");
                    try
                    {
                        printService.Print(finalPath, printerName);
                        printed = true;
                        Logger.LogToPlf($"Token {token}: printed successfully", isError: false);
                    }
                    catch (PrintException ex)
                    {
                        Logger.Error($"[token={token}] Print failed", ex);
                        Logger.LogToPlf($"Token {token}: print failed - {ex.Message}", isError: true);
                        JobTracker.Complete(jobId, false, finalPath, $"Failed to print: {ex.Message}");
                        WriteJson(context, 502, new { success = false, token, route, docType = info.DocumentType, outputPath = finalPath, signed, error = $"Failed to print: {ex.Message}" });
                        return;
                    }
                }

                JobTracker.Complete(jobId, true, finalPath, null);
                WriteJson(context, 200, new
                {
                    success = true,
                    token,
                    route,
                    docType = info.DocumentType,
                    fileName = info.FileName,
                    outputPath = finalPath,
                    signed,
                    printed
                });
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tokenFolder))
                        Directory.Delete(tokenFolder, recursive: true);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[token={token}] Could not clean up temp folder: {ex.Message}");
                }
            }
        }

        private static void WriteJson(HttpListenerContext context, int statusCode, object payload)
        {
            string json = JsonConvert.SerializeObject(payload);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
    }
}
