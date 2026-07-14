using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;

namespace MockDocumentServer
{
    internal class DocumentManifestEntry
    {
        public string FileName { get; set; }
        public string DocumentType { get; set; }
        public string DownloadUrl { get; set; }
    }

    internal class DocumentManifest
    {
        public string Token { get; set; }
        public List<DocumentManifestEntry> Documents { get; set; } = new List<DocumentManifestEntry>();
    }

    internal class Program
    {
        private const string ExpectedApiKey = "local-dev-key";
        private static readonly Regex ManifestRoute = new Regex(@"^/manifest/([^/]+)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DocumentRoute = new Regex(@"^/document/([^/]+)/([^/]+)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static void Main(string[] args)
        {
            int port = args.Length > 0 && int.TryParse(args[0], out int p) ? p : 9091;

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            Console.WriteLine($"Mock document server listening on http://localhost:{port}/ (expects header X-Api-Key: {ExpectedApiKey})");
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

            var manifestMatch = ManifestRoute.Match(path);
            if (manifestMatch.Success)
            {
                string token = manifestMatch.Groups[1].Value;
                WriteJson(context, 200, BuildManifest(token));
                return;
            }

            var documentMatch = DocumentRoute.Match(path);
            if (documentMatch.Success)
            {
                string token = documentMatch.Groups[1].Value;
                string fileName = Uri.UnescapeDataString(documentMatch.Groups[2].Value);
                var manifest = BuildManifest(token);
                var entry = manifest.Documents.FirstOrDefault(d => d.FileName == fileName);
                if (entry == null)
                {
                    WriteJson(context, 404, new { error = "unknown document for this token" });
                    return;
                }

                byte[] pdfBytes = FakePdfGenerator.Generate(token, entry.DocumentType, entry.FileName);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/pdf";
                context.Response.ContentLength64 = pdfBytes.Length;
                context.Response.OutputStream.Write(pdfBytes, 0, pdfBytes.Length);
                context.Response.OutputStream.Close();
                return;
            }

            WriteJson(context, 404, new { error = "unknown route. Expected /manifest/{token} or /document/{token}/{fileName}." });
        }

        // Deterministic per token: same token always yields the same document set (1 invoice + 1-4 labels).
        private static DocumentManifest BuildManifest(string token)
        {
            var random = new Random(token.GetHashCode());
            int labelCount = random.Next(1, 5);

            var manifest = new DocumentManifest { Token = token };
            manifest.Documents.Add(new DocumentManifestEntry
            {
                FileName = $"invoice_{token}.pdf",
                DocumentType = "invoice",
                DownloadUrl = $"/document/{token}/invoice_{token}.pdf"
            });

            for (int i = 1; i <= labelCount; i++)
            {
                manifest.Documents.Add(new DocumentManifestEntry
                {
                    FileName = $"label_{token}_{i}.pdf",
                    DocumentType = "label",
                    DownloadUrl = $"/document/{token}/label_{token}_{i}.pdf"
                });
            }

            return manifest;
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
