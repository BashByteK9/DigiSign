using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DigiSign
{
    public class HttpListenerService : IDisposable
    {
        private static readonly Regex RoutePattern = new Regex(@"^/(invoice|label)/([^/]*)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly int port;
        private readonly XmlData xmlData;
        private readonly IInvoiceDownloader downloader;
        private readonly DigitalSignatureService signatureService;

        private HttpListener listener;
        private Thread listenThread;
        private volatile bool stopping;

        public HttpListenerService(int port, XmlData xmlData, IInvoiceDownloader downloader)
        {
            this.port = port;
            this.xmlData = xmlData;
            this.downloader = downloader;
            this.signatureService = new DigitalSignatureService();
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
                    WriteJsonResponse(context, 500, false, null, "Internal server error");
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
                WriteJsonResponse(context, 404, false, null, "Unknown route. Expected /invoice/{token} or /label/{token}.");
                return;
            }

            string token = match.Groups[2].Value;
            if (string.IsNullOrWhiteSpace(token))
            {
                Logger.Warning($"Route matched but token is missing: {method} {path}");
                WriteJsonResponse(context, 400, false, null, "Token is required.");
                return;
            }

            DocumentType docType = match.Groups[1].Value.Equals("invoice", StringComparison.OrdinalIgnoreCase)
                ? DocumentType.Invoice
                : DocumentType.Label;

            ProcessSignRequest(context, docType, token);
        }

        private void ProcessSignRequest(HttpListenerContext context, DocumentType docType, string token)
        {
            string tempInputPath = null;
            try
            {
                tempInputPath = downloader.DownloadDocument(docType, token);
                Logger.Info($"Downloaded {docType} document for token '{token}' -> {tempInputPath}");
            }
            catch (DownloadException ex)
            {
                Logger.Error($"Download failed for {docType}/{token}", ex);
                WriteJsonResponse(context, 502, false, null, $"Failed to download document: {ex.Message}");
                return;
            }

            try
            {
                var cert = signatureService.LoadCertificate(xmlData.CommonName, xmlData.Pin, xmlData);
                if (cert == null)
                {
                    Logger.Error($"Certificate not found for CN '{xmlData.CommonName}' processing {docType}/{token}");
                    WriteJsonResponse(context, 500, false, null, "Signing certificate not available.");
                    return;
                }

                string outputFileName = $"{docType.ToString().ToLowerInvariant()}_{token}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                string outputPath = Path.Combine(xmlData.OutputFolderPath, outputFileName);
                var signatureConfig = new SignatureConfiguration(
                    xmlData.XCoordinate, xmlData.YCoordinate, xmlData.Width, xmlData.Height, xmlData.SignOnPage);

                Directory.CreateDirectory(xmlData.OutputFolderPath);
                signatureService.SignPdf(tempInputPath, outputPath, cert, signatureConfig, xmlData.Pin, xmlData.OutputFolderPath);

                Logger.Info($"Signed {docType}/{token} -> {outputPath}");
                WriteJsonResponse(context, 200, true, outputPath, null);
            }
            catch (Exception ex)
            {
                Logger.Error($"Signing failed for {docType}/{token}", ex);
                WriteJsonResponse(context, 500, false, null, $"Signing failed: {ex.Message}");
            }
            finally
            {
                if (tempInputPath != null)
                {
                    try { File.Delete(tempInputPath); } catch { }
                }
            }
        }

        private static void WriteJsonResponse(HttpListenerContext context, int statusCode, bool success, string outputPath, string error)
        {
            string json = success
                ? $"{{\"success\":true,\"outputPath\":\"{EscapeJson(outputPath)}\"}}"
                : $"{{\"success\":false,\"error\":\"{EscapeJson(error)}\"}}";

            byte[] buffer = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        private static string EscapeJson(string s)
        {
            return s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
        }
    }
}
