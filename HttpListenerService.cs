using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;

namespace DigiSign
{
    public class HttpListenerService : IDisposable
    {
        private static readonly Regex RoutePattern = new Regex(@"^/(invoice|invoice-print|invoice-sign|invoice-sign-print)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Hardware USB-token signing (PIN-protected private key) must not be hit concurrently.
        private static readonly SemaphoreSlim signingLock = new SemaphoreSlim(1, 1);

        private readonly int port;
        private readonly XmlData xmlData;
        private readonly IDocumentDownloader downloader;
        private readonly string licensePath;
        private readonly Action<string> onLicenseIssue;
        private readonly IPrintService printService;
        private readonly string printerName;
        private readonly bool enableOcspCheck;
        private readonly int ocspTimeoutSeconds;

        private HttpListener listener;
        private Thread listenThread;
        private volatile bool stopping;

        public HttpListenerService(int port, XmlData xmlData, IDocumentDownloader downloader, string licensePath, Action<string> onLicenseIssue,
            IPrintService printService, string printerName, bool enableOcspCheck, int ocspTimeoutSeconds)
        {
            this.port = port;
            this.xmlData = xmlData;
            this.downloader = downloader;
            this.licensePath = licensePath;
            this.onLicenseIssue = onLicenseIssue;
            this.printService = printService;
            this.printerName = printerName;
            this.enableOcspCheck = enableOcspCheck;
            this.ocspTimeoutSeconds = ocspTimeoutSeconds;
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
                    error = "Unknown route. Expected POST /invoice, /invoice-print, /invoice-sign, or /invoice-sign-print."
                });
                return;
            }

            if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning($"Rejected non-POST request to {path}");
                WriteJson(context, 405, new { success = false, error = "Expected POST with a JSON body." });
                return;
            }

            string route = match.Groups[1].Value.ToLowerInvariant();

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }

            SignRequestBatch batch;
            try
            {
                batch = JsonConvert.DeserializeObject<SignRequestBatch>(body);
            }
            catch (JsonException ex)
            {
                Logger.Warning($"Rejected request with invalid JSON body: {ex.Message}");
                WriteJson(context, 400, new { success = false, route, error = $"Invalid JSON body: {ex.Message}" });
                return;
            }

            if (batch == null || string.IsNullOrWhiteSpace(batch.ClientId))
            {
                Logger.Warning("Rejected request missing ClientId");
                WriteJson(context, 400, new { success = false, route, error = "ClientId is required." });
                return;
            }

            if (batch.Tokens == null || batch.Tokens.Count == 0)
            {
                Logger.Warning("Rejected request with no Tokens");
                WriteJson(context, 400, new { success = false, route, error = "At least one entry in Tokens is required." });
                return;
            }

            foreach (var t in batch.Tokens)
            {
                if (string.IsNullOrWhiteSpace(t?.TokenId))
                {
                    Logger.Warning("Rejected request with a blank TokenId in Tokens");
                    WriteJson(context, 400, new { success = false, route, error = "Each entry in Tokens requires a non-empty TokenId." });
                    return;
                }
            }

            bool doSign = route == "invoice-sign" || route == "invoice-sign-print";
            bool doPrint = route == "invoice-print" || route == "invoice-sign-print";

            var jobs = batch.Tokens.Select(t => new
            {
                Token = t,
                JobId = JobTracker.CreateJob(t.TokenId, route).JobId
            }).ToList();

            WriteJson(context, 202, new
            {
                success = true,
                clientId = batch.ClientId,
                route,
                accepted = jobs.Count,
                jobs = jobs.Select(j => new { tokenId = j.Token.TokenId, invoiceNo = j.Token.InvoiceNo, jobId = j.JobId })
            });

            ThreadPool.QueueUserWorkItem(_ => ProcessBatch(batch.ClientId, route, jobs.Select(j => (j.Token, j.JobId)).ToList(), doSign, doPrint));
        }

        private void ProcessBatch(string clientId, string route, List<(TokenInvoice Token, string JobId)> jobs, bool doSign, bool doPrint)
        {
            foreach (var job in jobs)
            {
                try
                {
                    ProcessSingleToken(clientId, job.Token, job.JobId, route, doSign, doPrint);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[token={job.Token.TokenId}] Unhandled exception while processing batch item", ex);
                    JobTracker.Complete(job.JobId, false, null, $"Unhandled error: {ex.Message}");
                }
            }
        }

        private void ProcessSingleToken(string clientId, TokenInvoice tokenInvoice, string jobId, string route, bool doSign, bool doPrint)
        {
            string tokenId = tokenInvoice.TokenId;
            string invoiceNo = tokenInvoice.InvoiceNo;
            string tokenFolder = Path.Combine(Path.GetTempPath(), $"digisign_{tokenId}_{DateTime.Now:yyyyMMddHHmmssfff}");
            try
            {
                JobTracker.SetDocumentInfo(jobId, "invoice", $"{invoiceNo}.pdf");

                Logger.Info($"[token={tokenId}] Fetching invoice document (InvoiceNo={invoiceNo})");
                JobTracker.UpdateStage(jobId, JobStage.Fetching, "Fetching invoice document...");
                byte[] documentBytes;
                try
                {
                    documentBytes = downloader.FetchInvoiceDocument(clientId, tokenId);
                }
                catch (DownloadException ex)
                {
                    Logger.Error($"[token={tokenId}] Fetch failed", ex);
                    Logger.LogToPlf($"Token {tokenId}: failed to fetch invoice - {ex.Message}", isError: true);
                    JobTracker.Complete(jobId, false, null, $"Failed to fetch invoice document: {ex.Message}");
                    return;
                }

                Directory.CreateDirectory(tokenFolder);
                string safeFileName = string.Join("_", $"{invoiceNo}.pdf".Split(Path.GetInvalidFileNameChars()));
                string localFilePath = Path.Combine(tokenFolder, safeFileName);
                File.WriteAllBytes(localFilePath, documentBytes);

                string finalPath;
                List<string> allOutputPaths;
                bool printAllCopies = false;

                if (doSign)
                {
                    if (!LicenseManager.ValidateLicense(licensePath))
                    {
                        string msg = $"Signing request for token '{tokenId}' was rejected: no valid license is installed. Run 'DigiSign.exe /settings' or contact support for a license.";
                        Logger.Error(msg);
                        Logger.LogToPlf($"Token {tokenId}: valid license required for signing", isError: true);
                        JobTracker.Complete(jobId, false, null, "Valid license required for signing.");
                        onLicenseIssue(msg);
                        return;
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
                        result = BatchSigner.SignFiles(new[] { localFilePath }, xmlData.OutputFolderPath, xmlData.CommonName, xmlData.Pin, signatureConfig, xmlData,
                            new JobTrackingBatchSignProgress(jobId));
                    }
                    finally
                    {
                        signingLock.Release();
                    }

                    if (result.CertificateError != null)
                    {
                        Logger.LogToPlf($"Token {tokenId}: {result.CertificateError}", isError: true);
                        JobTracker.Complete(jobId, false, null, result.CertificateError);
                        return;
                    }

                    var fileResult = result.FileResults.Count > 0 ? result.FileResults[0] : null;
                    if (fileResult == null || !fileResult.Success)
                    {
                        string err = fileResult?.Error ?? "Unknown signing failure";
                        Logger.LogToPlf($"Token {tokenId}: signing failed - {err}", isError: true);
                        JobTracker.Complete(jobId, false, null, err);
                        return;
                    }

                    allOutputPaths = fileResult.OutputPaths;
                    finalPath = allOutputPaths[0];
                    printAllCopies = signatureConfig.PrintAllCopies;
                    Logger.LogToPlf($"Token {tokenId}: signed successfully", isError: false);
                }
                else
                {
                    Directory.CreateDirectory(xmlData.OutputFolderPath);
                    string destPath = Path.Combine(xmlData.OutputFolderPath, Path.GetFileName(localFilePath));
                    File.Copy(localFilePath, destPath, overwrite: true);
                    finalPath = destPath;
                    allOutputPaths = new List<string> { finalPath };
                    Logger.LogToPlf($"Token {tokenId}: fetched (unsigned)", isError: false);
                }

                if (doPrint)
                {
                    JobTracker.UpdateStage(jobId, JobStage.Printing, $"Printing to {(string.IsNullOrWhiteSpace(printerName) ? "default printer" : printerName)}...");
                    var pathsToPrint = printAllCopies ? allOutputPaths : new List<string> { finalPath };
                    try
                    {
                        foreach (var pathToPrint in pathsToPrint)
                        {
                            printService.Print(pathToPrint, printerName);
                        }
                        Logger.LogToPlf($"Token {tokenId}: printed successfully", isError: false);
                    }
                    catch (PrintException ex)
                    {
                        Logger.Error($"[token={tokenId}] Print failed", ex);
                        Logger.LogToPlf($"Token {tokenId}: print failed - {ex.Message}", isError: true);
                        JobTracker.Complete(jobId, false, finalPath, $"Failed to print: {ex.Message}");
                        return;
                    }
                }

                try
                {
                    byte[] finalBytes = File.ReadAllBytes(finalPath);
                    downloader.PostSignedInvoiceCallback(clientId, tokenId, invoiceNo, finalBytes);
                }
                catch (CallbackException ex)
                {
                    Logger.Error($"[token={tokenId}] invoice-signed callback failed", ex);
                    Logger.LogToPlf($"Token {tokenId}: invoice-signed callback failed - {ex.Message}", isError: true);
                }

                JobTracker.Complete(jobId, true, finalPath, null);
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
                    Logger.Warning($"[token={tokenId}] Could not clean up temp folder: {ex.Message}");
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
