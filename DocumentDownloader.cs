using System;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;

namespace DigiSign
{
    public class DocumentInfo
    {
        public string Token { get; set; }
        public string FileName { get; set; }
        public string DocumentType { get; set; } // "invoice" | "label"
        public string DownloadUrl { get; set; }
    }

    public class DownloadException : Exception
    {
        public DownloadException(string message, Exception inner = null) : base(message, inner) { }
    }

    public interface IDocumentDownloader
    {
        /// <summary>
        /// Fetches metadata (doctype, filename, download URL) for the single document tied to a token.
        /// Throws DownloadException on failure.
        /// </summary>
        DocumentInfo FetchInfo(string token);

        /// <summary>
        /// Downloads the document described by info into destinationFolder and returns the local file path.
        /// Throws DownloadException on failure.
        /// </summary>
        string Download(DocumentInfo info, string destinationFolder);
    }

    // TODO: PLACEHOLDER - the real invoice/label document-provider API contract (endpoint shape,
    // auth scheme, response format) is not yet known. This targets the local MockDocumentServer's
    // /info/{token} and /document/{token} routes; replace the request-building logic below once the
    // real API is specified - the IDocumentDownloader shape shouldn't need to change.
    public class HttpDocumentDownloader : IDocumentDownloader
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private readonly string baseUrl;
        private readonly string apiKey;

        public HttpDocumentDownloader(string baseUrl, string apiKey)
        {
            this.baseUrl = baseUrl;
            this.apiKey = apiKey;
        }

        public DocumentInfo FetchInfo(string token)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new DownloadException("Invoice/Label API base URL is not configured. Set it under the API Settings tab.");

            string url = $"{baseUrl.TrimEnd('/')}/info/{Uri.EscapeDataString(token)}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    if (!string.IsNullOrEmpty(apiKey))
                        request.Headers.Add("X-Api-Key", apiKey); // TODO: confirm real auth header/scheme

                    var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return JsonConvert.DeserializeObject<DocumentInfo>(json);
                }
            }
            catch (Exception ex) when (!(ex is DownloadException))
            {
                throw new DownloadException($"Failed to fetch document info for token '{token}': {ex.Message}", ex);
            }
        }

        public string Download(DocumentInfo info, string destinationFolder)
        {
            string url = info.DownloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? info.DownloadUrl
                : $"{baseUrl.TrimEnd('/')}{info.DownloadUrl}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    if (!string.IsNullOrEmpty(apiKey))
                        request.Headers.Add("X-Api-Key", apiKey);

                    var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();

                    string safeFileName = string.Join("_", (info.FileName ?? $"{Guid.NewGuid():N}.pdf").Split(Path.GetInvalidFileNameChars()));
                    string localPath = Path.Combine(destinationFolder, safeFileName);
                    File.WriteAllBytes(localPath, bytes);
                    return localPath;
                }
            }
            catch (Exception ex) when (!(ex is DownloadException))
            {
                throw new DownloadException($"Failed to download '{info.FileName}' ({info.DownloadUrl}): {ex.Message}", ex);
            }
        }
    }
}
