using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;

namespace MockDocumentServer
{
    internal class DocumentInfo
    {
        public string Token { get; set; }
        public string FileName { get; set; }
        public string DocumentType { get; set; }
        public string DownloadUrl { get; set; }
    }

    internal class Program
    {
        private const string ExpectedApiKey = "local-dev-key";
        private static readonly Regex InfoRoute = new Regex(@"^/info/([^/]+)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DocumentRoute = new Regex(@"^/document/([^/]+)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static void Main(string[] args)
        {
            int port = args.Length > 0 && int.TryParse(args[0], out int p) ? p : 9091;

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            Console.WriteLine($"Mock document server listening on http://localhost:{port}/ (expects header X-Api-Key: {ExpectedApiKey})");
            Console.WriteLine("Routes: GET /info/{token}, GET /document/{token}");
            Console.WriteLine("Press Ctrl+C to stop.");

            var stopping = false;
            var shutdownSignal = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                shutdownSignal.Set();
            };
            var listenThread = new Thread(() =>
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
                        break;
                    }

                    var localContext = context;
                    ThreadPool.QueueUserWorkItem(_ => HandleRequestSafe(localContext));
                }
            }) { IsBackground = true };
            listenThread.Start();

            shutdownSignal.Wait();
            stopping = true;
            listener.Stop();
            listener.Close();
        }

        private static void HandleRequestSafe(HttpListenerContext context)
        {
            try
            {
                HandleRequest(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception: {ex}");
                try { WriteJson(context, 500, new { error = "internal server error" }); }
                catch { /* response may already be closed */ }
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath;
            string apiKey = context.Request.Headers["X-Api-Key"];

            Console.WriteLine($"{context.Request.HttpMethod} {path}");

            if (apiKey != ExpectedApiKey)
            {
                WriteJson(context, 401, new { error = "invalid api key" });
                return;
            }

            var infoMatch = InfoRoute.Match(path);
            if (infoMatch.Success)
            {
                string token = infoMatch.Groups[1].Value;
                WriteJson(context, 200, BuildInfo(token));
                return;
            }

            var documentMatch = DocumentRoute.Match(path);
            if (documentMatch.Success)
            {
                string token = documentMatch.Groups[1].Value;
                var info = BuildInfo(token);

                byte[] pdfBytes = FakePdfGenerator.Generate(token, info.DocumentType, info.FileName);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/pdf";
                context.Response.ContentLength64 = pdfBytes.Length;
                context.Response.OutputStream.Write(pdfBytes, 0, pdfBytes.Length);
                context.Response.OutputStream.Close();
                return;
            }

            WriteJson(context, 404, new { error = "unknown route. Expected /info/{token} or /document/{token}." });
        }

        // Deterministic per token: same token always yields the same single document (doctype/filename).
        private static DocumentInfo BuildInfo(string token)
        {
            var random = new Random(token.GetHashCode());
            string docType = random.NextDouble() < 0.7 ? "invoice" : "label";
            string fileName = $"{docType}_{token}.pdf";

            return new DocumentInfo
            {
                Token = token,
                FileName = fileName,
                DocumentType = docType,
                DownloadUrl = $"/document/{token}"
            };
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
