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
        private static readonly Regex RoutePattern = new Regex(@"^/(invoice|invoice-print|invoice-sign|invoice-sign-print|label-print)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly int port;
        private readonly XmlData xmlData;
        private readonly IDocumentDownloader downloader;
        private readonly string licensePath;
        private readonly Action<string> onLicenseIssue;
        private readonly IPrintService printService;
        private readonly string printerName;
        private readonly bool enableOcspCheck;
        private readonly int ocspTimeoutSeconds;
        private readonly ILabelPrintService labelPrintService;

        private HttpListener listener;
        private Thread listenThread;
        private volatile bool stopping;

        public HttpListenerService(int port, XmlData xmlData, IDocumentDownloader downloader, string licensePath, Action<string> onLicenseIssue,
            IPrintService printService, string printerName, bool enableOcspCheck, int ocspTimeoutSeconds, ILabelPrintService labelPrintService)
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
            this.labelPrintService = labelPrintService;
        }

        public void Start()
        {
            // A relaunch-triggered restart stops the old process and starts a new one, which race
            // to release/rebind the same port across two OS processes with no direct handshake -
            // retry briefly before giving up.
            const int maxAttempts = 3;
            const int retryDelayMs = 300;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add($"http://localhost:{port}/");
                    listener.Start();
                    break;
                }
                catch (HttpListenerException) when (attempt < maxAttempts)
                {
                    Logger.Warning($"Port {port} not yet available (attempt {attempt}/{maxAttempts}) - retrying in {retryDelayMs}ms");
                    Thread.Sleep(retryDelayMs);
                }
            }

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

            ApplyCorsHeaders(context);

            if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 204;
                context.Response.OutputStream.Close();
                return;
            }

            if (string.Equals(path, "/label-printers", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, "/label-printers/", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warning($"Rejected non-GET request to {path}");
                    WriteJson(context, 405, new { success = false, error = "Expected GET." });
                    return;
                }

                HandleListPrintersRequest(context);
                return;
            }

            if (string.Equals(path, "/heartbeat", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, "/heartbeat/", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warning($"Rejected non-GET request to {path}");
                    WriteJson(context, 405, new { success = false, error = "Expected GET." });
                    return;
                }

                HandleHeartbeatRequest(context);
                return;
            }

            var match = RoutePattern.Match(path);
            if (!match.Success)
            {
                Logger.Warning($"Unrecognized route requested: {method} {path}");
                WriteJson(context, 404, new
                {
                    success = false,
                    error = "Unknown route. Expected POST /invoice, /invoice-print, /invoice-sign, /invoice-sign-print, or /label-print."
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

            if (route == "label-print")
            {
                HandleLabelPrintRequest(context);
                return;
            }

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
                JobId = JobTracker.CreateJob(t.TokenId, route, batch.ClientId, t.InvoiceNo, JobSource.Listener, doSign: doSign, doPrint: doPrint).JobId
            }).ToList();

            WriteJson(context, 202, new
            {
                success = true,
                clientId = batch.ClientId,
                route,
                accepted = jobs.Count,
                jobs = jobs.Select(j => new { tokenId = j.Token.TokenId, invoiceNo = j.Token.InvoiceNo, jobId = j.JobId })
            });

            ThreadPool.QueueUserWorkItem(_ => ProcessBatch(jobs.Select(j => j.JobId).ToList()));
        }

        private void ProcessBatch(List<string> jobIds)
        {
            foreach (var jobId in jobIds)
            {
                try
                {
                    ProcessSingleToken(jobId);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[job={jobId}] Unhandled exception while processing batch item", ex);
                    JobTracker.Complete(jobId, false, null, $"Unhandled error: {ex.Message}");
                }
            }
        }

        /// <summary>Entry point for JobTracker.RegisterResumeHandler(JobSource.Listener, ...) - re-enters the same pipeline a fresh request uses, reading everything it needs from the persisted JobRecord.</summary>
        public void ProcessResumedJob(string jobId) => ProcessSingleToken(jobId);

        /// <summary>
        /// Drives a single token through fetch -> sign -> print -> callback. Reads all context (ClientId,
        /// TokenId, InvoiceNo, DoSign, DoPrint) from the persisted JobRecord rather than taking it as
        /// parameters, so this same method serves both a fresh HTTP-triggered run and a Resume click -
        /// checkpoint-gated: each step is skipped if the record shows it already completed, and cancellation
        /// is only checked between steps (never interrupts a step already running).
        /// </summary>
        private void ProcessSingleToken(string jobId)
        {
            var job = JobTracker.GetJob(jobId);
            if (job == null)
            {
                Logger.Error($"[job={jobId}] Job record not found - aborting");
                return;
            }

            string tokenId = job.Token;
            string invoiceNo = job.InvoiceNo;
            string clientId = job.ClientId;
            bool doSign = job.DoSign;
            bool doPrint = job.DoPrint;
            string tokenFolder = Path.Combine(Path.GetTempPath(), $"digisign_{tokenId}_{DateTime.Now:yyyyMMddHHmmssfff}");

            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            JobTracker.SetOwner(jobId, currentProcess.Id, currentProcess.StartTime.ToUniversalTime());

            if (job.CancellationRequested)
            {
                Logger.Info($"[token={tokenId}] Job {jobId} was cancelled before processing began");
                JobTracker.Cancel(jobId);
                return;
            }

            // Deliberately not gated on job.Stage: JobRecoveryService collapses whatever stage a crashed
            // job was in (Signed, Printing, Printed, ...) down to the single generic Interrupted stage,
            // discarding exactly how far it got. PathsToPrint is only ever populated once signing (or the
            // copy-without-signing equivalent) has actually succeeded, so its mere presence - with every
            // path still present on disk - is the reliable, stage-independent signal that fetch+sign can
            // be skipped on resume.
            bool alreadySigned = job.PathsToPrint != null && job.PathsToPrint.Count > 0
                && job.PathsToPrint.All(File.Exists);

            string finalPath;
            List<string> pathsToPrint;

            try
            {
                if (alreadySigned)
                {
                    Logger.Info($"[token={tokenId}] Resuming job {jobId} - skipping fetch+sign, reusing existing output");
                    pathsToPrint = job.PathsToPrint;
                    finalPath = pathsToPrint[0];
                }
                else
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

                    if (doSign)
                    {
                        var signResult = SigningPipeline.SignSingleFile(
                            localFilePath, xmlData, enableOcspCheck, ocspTimeoutSeconds, licensePath,
                            out string licenseIssue, new JobTrackingBatchSignProgress(jobId));

                        if (licenseIssue != null)
                        {
                            Logger.Error($"[token={tokenId}] {licenseIssue}");
                            Logger.LogToPlf($"Token {tokenId}: valid license required for signing", isError: true);
                            JobTracker.Complete(jobId, false, null, licenseIssue);
                            onLicenseIssue($"Signing request for token '{tokenId}' was rejected: no valid license is installed. Run 'DigiSign.exe /settings' or contact support for a license.");
                            return;
                        }

                        if (!signResult.Success)
                        {
                            Logger.LogToPlf($"Token {tokenId}: {signResult.Error}", isError: true);
                            JobTracker.Complete(jobId, false, null, signResult.Error);
                            return;
                        }

                        pathsToPrint = signResult.PrintAllCopies
                            ? signResult.OutputPaths
                            : new List<string> { signResult.OutputPaths[0] };
                        finalPath = signResult.OutputPaths[0];
                        Logger.LogToPlf($"Token {tokenId}: signed successfully", isError: false);
                    }
                    else
                    {
                        Directory.CreateDirectory(xmlData.OutputFolderPath);
                        string destPath = Path.Combine(xmlData.OutputFolderPath, Path.GetFileName(localFilePath));
                        File.Copy(localFilePath, destPath, overwrite: true);
                        finalPath = destPath;
                        pathsToPrint = new List<string> { finalPath };
                        Logger.LogToPlf($"Token {tokenId}: fetched (unsigned)", isError: false);
                    }

                    JobTracker.SetSigned(jobId, wasSigned: doSign, pathsToPrint, printerName);
                }

                // Cooperative cancellation gate - only checked between steps, never interrupts a step already running.
                if (JobTracker.GetJob(jobId)?.CancellationRequested == true)
                {
                    Logger.Info($"[token={tokenId}] Job {jobId} cancelled before printing");
                    JobTracker.Cancel(jobId);
                    return;
                }

                bool alreadyPrinted = JobTracker.GetJob(jobId)?.Stage == JobStage.Printed;

                if (doPrint && !alreadyPrinted)
                {
                    JobTracker.UpdateStage(jobId, JobStage.Printing, $"Printing to {(string.IsNullOrWhiteSpace(printerName) ? "default printer" : printerName)}...");
                    try
                    {
                        foreach (var pathToPrint in pathsToPrint)
                        {
                            if (!File.Exists(pathToPrint))
                            {
                                Logger.Warning($"[token={tokenId}] Print input '{pathToPrint}' is missing (deleted since signing?) - cannot print");
                                JobTracker.Complete(jobId, false, finalPath, $"Signed file missing before printing: {pathToPrint}");
                                return;
                            }
                            printService.Print(pathToPrint, printerName);
                        }
                        Logger.LogToPlf($"Token {tokenId}: printed successfully", isError: false);
                        JobTracker.SetPrinted(jobId);
                    }
                    catch (PrintException ex)
                    {
                        Logger.Error($"[token={tokenId}] Print failed", ex);
                        Logger.LogToPlf($"Token {tokenId}: print failed - {ex.Message}", isError: true);
                        JobTracker.Complete(jobId, false, finalPath, $"Failed to print: {ex.Message}");
                        return;
                    }
                }

                // Cancellation gate before callback.
                if (JobTracker.GetJob(jobId)?.CancellationRequested == true)
                {
                    Logger.Info($"[token={tokenId}] Job {jobId} cancelled before callback");
                    JobTracker.Cancel(jobId);
                    return;
                }

                bool alreadyCalledBack = JobTracker.GetJob(jobId)?.CallbackSuccess == true;
                if (!alreadyCalledBack)
                {
                    try
                    {
                        if (!File.Exists(finalPath))
                        {
                            Logger.Warning($"[token={tokenId}] Final output '{finalPath}' is missing - cannot post callback");
                            JobTracker.SetCallbackResult(jobId, false, $"Output file missing: {finalPath}");
                        }
                        else
                        {
                            byte[] finalBytes = File.ReadAllBytes(finalPath);
                            downloader.PostSignedInvoiceCallback(clientId, tokenId, invoiceNo, finalBytes);
                            JobTracker.SetCallbackResult(jobId, true, null);
                        }
                    }
                    catch (CallbackException ex)
                    {
                        Logger.Error($"[token={tokenId}] invoice-signed callback failed", ex);
                        Logger.LogToPlf($"Token {tokenId}: invoice-signed callback failed - {ex.Message}", isError: true);
                        JobTracker.SetCallbackResult(jobId, false, ex.Message);
                    }
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

        /// <summary>
        /// Handles POST /label-print: prints a raw ZPL command string directly to a printer, synchronously.
        /// Unlike the invoice routes there's no fetch/sign step, so the response reflects the actual
        /// print outcome instead of a 202-and-poll pattern.
        /// </summary>
        private void HandleLabelPrintRequest(HttpListenerContext context)
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }

            LabelPrintRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<LabelPrintRequest>(body);
            }
            catch (JsonException ex)
            {
                Logger.Warning($"Rejected label-print request with invalid JSON body: {ex.Message}");
                WriteJson(context, 400, new { success = false, route = "label-print", error = $"Invalid JSON body: {ex.Message}" });
                return;
            }

            string validationError = LabelPrintPipeline.Validate(request);
            if (validationError != null)
            {
                Logger.Warning($"Rejected label-print request: {validationError}");
                WriteJson(context, 400, new { success = false, route = "label-print", error = validationError });
                return;
            }

            string printerLabel = string.IsNullOrWhiteSpace(request.Printer) ? "(default printer)" : request.Printer;
            string jobId = JobTracker.CreateJob(
                token: Guid.NewGuid().ToString("N"), route: "label-print", clientId: printerLabel, invoiceNo: null,
                source: JobSource.LabelPrint, doSign: false, doPrint: true).JobId;

            Logger.Info($"[job={jobId}] Printing label to {printerLabel}");
            JobTracker.UpdateStage(jobId, JobStage.Printing, $"Printing to {printerLabel}...");

            var result = LabelPrintPipeline.PrintLabel(request, labelPrintService);

            if (result.Success)
            {
                JobTracker.SetPrinted(jobId);
                JobTracker.Complete(jobId, true, null, null);
                Logger.LogToPlf($"Label print job {jobId}: printed successfully", isError: false);
                WriteJson(context, 200, new { success = true, jobId });
            }
            else
            {
                JobTracker.Complete(jobId, false, null, result.Error);
                Logger.Error($"[job={jobId}] Label print failed: {result.Error}");
                Logger.LogToPlf($"Label print job {jobId}: failed - {result.Error}", isError: true);
                WriteJson(context, 500, new { success = false, jobId, error = result.Error });
            }
        }

        /// <summary>Handles GET /heartbeat: confirms the listener process itself is alive and responding - deliberately no license/printer/JobTracker checks.</summary>
        private static void HandleHeartbeatRequest(HttpListenerContext context)
        {
            WriteJson(context, 200, new { success = true, status = "ok" });
        }

        /// <summary>Handles GET /label-printers: lists installed printer names so a caller can discover a valid value for /label-print's Printer field.</summary>
        private static void HandleListPrintersRequest(HttpListenerContext context)
        {
            var printers = System.Drawing.Printing.PrinterSettings.InstalledPrinters.Cast<string>().ToList();
            WriteJson(context, 200, new { success = true, printers });
        }

        private static void ApplyCorsHeaders(HttpListenerContext context)
        {
            string origin = context.Request.Headers["Origin"];
            context.Response.Headers["Access-Control-Allow-Origin"] = string.IsNullOrEmpty(origin) ? "*" : origin;
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
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
