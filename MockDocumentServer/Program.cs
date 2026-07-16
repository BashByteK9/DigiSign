using System;
using System.IO;
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

        [JsonIgnore]
        public string FilePath { get; set; }
    }

    internal class InvoiceFetchRequest
    {
        public string ClientId { get; set; }
        public string TokenId { get; set; }
    }

    internal class InvoiceSignedCallback
    {
        public string ClientId { get; set; }
        public string TokenId { get; set; }
        public string InvoiceNo { get; set; }

        [JsonProperty("signed-pdf")]
        public string SignedPdfBase64 { get; set; }
    }

    internal class Program
    {
        private const string ExpectedApiKey = "local-dev-key";
        private const string PdfSourceFolder = @"C:\Users\Public\pdfs";
        private static readonly Regex InvoiceRoute = new Regex(@"^/invoice/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex InvoiceSignedRoute = new Regex(@"^/invoice-signed/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static void Main(string[] args)
        {
            int port = args.Length > 0 && int.TryParse(args[0], out int p) ? p : 9091;

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            Console.WriteLine($"Mock document server listening on http://localhost:{port}/ (expects header X-Api-Key: {ExpectedApiKey})");
            Console.WriteLine($"Serving PDFs from: {PdfSourceFolder}");
            Console.WriteLine("Routes: POST /invoice/, POST /invoice-signed/");
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

            if (InvoiceRoute.IsMatch(path))
            {
                string body = ReadBody(context);
                InvoiceFetchRequest fetchRequest;
                try
                {
                    fetchRequest = JsonConvert.DeserializeObject<InvoiceFetchRequest>(body);
                }
                catch (JsonException ex)
                {
                    WriteJson(context, 400, new { error = $"invalid JSON body: {ex.Message}" });
                    return;
                }

                if (fetchRequest == null || string.IsNullOrWhiteSpace(fetchRequest.TokenId))
                {
                    WriteJson(context, 400, new { error = "TokenId is required." });
                    return;
                }

                DocumentInfo info;
                try
                {
                    info = BuildInfo(fetchRequest.TokenId);
                }
                catch (InvalidOperationException ex)
                {
                    WriteJson(context, 500, new { error = ex.Message });
                    return;
                }

                byte[] pdfBytes = File.ReadAllBytes(info.FilePath);
                Console.WriteLine($"  -> /invoice/ ClientId={fetchRequest.ClientId} TokenId={fetchRequest.TokenId} -> {Path.GetFileName(info.FilePath)}");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/pdf";
                context.Response.ContentLength64 = pdfBytes.Length;
                context.Response.OutputStream.Write(pdfBytes, 0, pdfBytes.Length);
                context.Response.OutputStream.Close();
                return;
            }

            if (InvoiceSignedRoute.IsMatch(path))
            {
                string body = ReadBody(context);
                InvoiceSignedCallback callback;
                try
                {
                    callback = JsonConvert.DeserializeObject<InvoiceSignedCallback>(body);
                }
                catch (JsonException ex)
                {
                    WriteJson(context, 400, new { error = $"invalid JSON body: {ex.Message}" });
                    return;
                }

                int base64Length = callback?.SignedPdfBase64?.Length ?? 0;
                Console.WriteLine($"  -> /invoice-signed/ ClientId={callback?.ClientId} TokenId={callback?.TokenId} InvoiceNo={callback?.InvoiceNo} signed-pdf length={base64Length}");
                WriteJson(context, 200, new { success = true });
                return;
            }

            WriteJson(context, 404, new { error = "unknown route. Expected POST /invoice/ or POST /invoice-signed/." });
        }

        // Deterministic per token: same token always yields the same file from PdfSourceFolder.
        // Doctype is inferred from the filename ("INV*" -> invoice, anything else -> label).
        private static DocumentInfo BuildInfo(string token)
        {
            string[] files = Directory.Exists(PdfSourceFolder)
                ? Directory.GetFiles(PdfSourceFolder, "*.pdf")
                : new string[0];

            if (files.Length == 0)
                throw new InvalidOperationException($"No PDF files found in {PdfSourceFolder}");

            var random = new Random(token.GetHashCode());
            string filePath = files[random.Next(files.Length)];
            string fileName = Path.GetFileName(filePath);
            string docType = fileName.StartsWith("INV", StringComparison.OrdinalIgnoreCase) ? "invoice" : "label";

            return new DocumentInfo
            {
                Token = token,
                FileName = fileName,
                DocumentType = docType,
                DownloadUrl = $"/document/{token}",
                FilePath = filePath
            };
        }

        private static string ReadBody(HttpListenerContext context)
        {
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                return reader.ReadToEnd();
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
