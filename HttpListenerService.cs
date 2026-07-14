using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DigiSign
{
    public class HttpListenerService : IDisposable
    {
        private static readonly Regex RoutePattern = new Regex(@"^/process/([^/]+)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly int port;
        private readonly XmlData xmlData;
        private readonly IDocumentSetDownloader downloader;
        private readonly string licensePath;
        private readonly Action<string> onLicenseIssue;

        private HttpListener listener;
        private Thread listenThread;
        private volatile bool stopping;

        public HttpListenerService(int port, XmlData xmlData, IDocumentSetDownloader downloader, string licensePath, Action<string> onLicenseIssue)
        {
            this.port = port;
            this.xmlData = xmlData;
            this.downloader = downloader;
            this.licensePath = licensePath;
            this.onLicenseIssue = onLicenseIssue;
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
                    WriteJsonError(context, 500, null, "Internal server error");
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
                WriteJsonError(context, 404, null, "Unknown route. Expected /process/{token}.");
                return;
            }

            string token = match.Groups[1].Value;
            if (!IsValidToken(token, out string reason))
            {
                Logger.Warning($"Rejected request with invalid token: {reason}");
                WriteJsonError(context, 400, token, reason);
                return;
            }

            ProcessTokenRequest(context, token);
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

        private void ProcessTokenRequest(HttpListenerContext context, string token)
        {
            if (!LicenseManager.ValidateLicense(licensePath))
            {
                string msg = $"Signing request for token '{token}' was rejected: no valid license is installed. Run 'DigiSign.exe /settings' or contact support for a license.";
                Logger.Error(msg);
                WriteJsonError(context, 403, token, "Valid license required for signing.");
                onLicenseIssue(msg);
                return;
            }

            string tokenFolder = Path.Combine(Path.GetTempPath(), $"digisign_{token}_{DateTime.Now:yyyyMMddHHmmssfff}");
            try
            {
                Logger.Info($"[token={token}] Fetching document manifest");
                DocumentManifest manifest;
                try
                {
                    manifest = downloader.FetchManifest(token);
                }
                catch (DownloadException ex)
                {
                    Logger.Error($"[token={token}] Manifest fetch failed", ex);
                    WriteJsonError(context, 502, token, $"Failed to fetch document manifest: {ex.Message}");
                    return;
                }

                if (manifest == null || manifest.Documents == null || manifest.Documents.Count == 0)
                {
                    Logger.Warning($"[token={token}] Manifest returned zero documents - nothing to sign");
                    WriteJsonError(context, 404, token, "No documents found for this token.");
                    return;
                }

                Directory.CreateDirectory(tokenFolder);

                Logger.Info($"[token={token}] Downloading {manifest.Documents.Count} document(s) into {tokenFolder}");
                System.Collections.Generic.List<string> localFiles;
                try
                {
                    localFiles = downloader.DownloadAll(manifest, tokenFolder);
                }
                catch (DownloadException ex)
                {
                    Logger.Error($"[token={token}] Download failed", ex);
                    WriteJsonError(context, 502, token, $"Failed to download documents: {ex.Message}");
                    return;
                }

                var signatureConfig = new SignatureConfiguration(
                    xmlData.XCoordinate, xmlData.YCoordinate, xmlData.Width, xmlData.Height, xmlData.SignOnPage);

                var result = BatchSigner.SignFiles(localFiles, xmlData.OutputFolderPath, xmlData.CommonName, xmlData.Pin, signatureConfig, xmlData, progress: null);

                if (result.CertificateError != null)
                {
                    Logger.LogToPlf($"Token {token}: {result.CertificateError}", isError: true);
                    WriteJsonError(context, 500, token, result.CertificateError);
                    return;
                }

                Logger.LogToPlf($"Token {token}: {result.SuccessCount} succeeded, {result.FailCount} failed",
                    isError: result.SuccessCount == 0 && result.FailCount > 0);

                WriteBatchResultResponse(context, token, result);
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

        private static void WriteBatchResultResponse(HttpListenerContext context, string token, BatchSignResult result)
        {
            bool success = result.FailCount == 0 && result.SuccessCount > 0;

            var resultsJson = string.Join(",", result.FileResults.Select(r => r.Success
                ? $"{{\"fileName\":\"{EscapeJson(r.FileName)}\",\"success\":true,\"outputPath\":\"{EscapeJson(r.OutputPath)}\"}}"
                : $"{{\"fileName\":\"{EscapeJson(r.FileName)}\",\"success\":false,\"error\":\"{EscapeJson(r.Error)}\"}}"));

            string json = $"{{\"success\":{(success ? "true" : "false")},\"token\":\"{EscapeJson(token)}\"," +
                $"\"successCount\":{result.SuccessCount},\"failCount\":{result.FailCount},\"results\":[{resultsJson}]}}";

            WriteJson(context, 200, json);
        }

        private static void WriteJsonError(HttpListenerContext context, int statusCode, string token, string error)
        {
            string json = token != null
                ? $"{{\"success\":false,\"token\":\"{EscapeJson(token)}\",\"error\":\"{EscapeJson(error)}\"}}"
                : $"{{\"success\":false,\"error\":\"{EscapeJson(error)}\"}}";

            WriteJson(context, statusCode, json);
        }

        private static void WriteJson(HttpListenerContext context, int statusCode, string json)
        {
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
