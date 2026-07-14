using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;

namespace DigiSign
{
    public class DocumentManifestEntry
    {
        public string FileName { get; set; }
        public string DocumentType { get; set; }
        public string DownloadUrl { get; set; }
    }

    public class DocumentManifest
    {
        public string Token { get; set; }
        public List<DocumentManifestEntry> Documents { get; set; } = new List<DocumentManifestEntry>();
    }

    public class DownloadException : Exception
    {
        public DownloadException(string message, Exception inner = null) : base(message, inner) { }
    }

    public interface IDocumentSetDownloader
    {
        /// <summary>
        /// Fetches the manifest describing every document available for a token.
        /// Throws DownloadException on failure.
        /// </summary>
        DocumentManifest FetchManifest(string token);

        /// <summary>
        /// Downloads every document in the manifest into destinationFolder and
        /// returns the local file paths. Throws DownloadException on failure.
        /// </summary>
        List<string> DownloadAll(DocumentManifest manifest, string destinationFolder);
    }

    // TODO: PLACEHOLDER - the real invoice/label document-provider API contract (endpoint shape,
    // auth scheme, response format) is not yet known. This targets the local MockDocumentServer's
    // /manifest/{token} and /document/{token}/{fileName} routes; replace the request-building
    // logic below once the real API is specified - the IDocumentSetDownloader shape shouldn't need to change.
    public class HttpDocumentSetDownloader : IDocumentSetDownloader
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private readonly string baseUrl;
        private readonly string apiKey;

        public HttpDocumentSetDownloader(string baseUrl, string apiKey)
        {
            this.baseUrl = baseUrl;
            this.apiKey = apiKey;
        }

        public DocumentManifest FetchManifest(string token)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new DownloadException("Invoice/Label API base URL is not configured. Set it under the API Settings tab.");

            string url = $"{baseUrl.TrimEnd('/')}/manifest/{Uri.EscapeDataString(token)}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    if (!string.IsNullOrEmpty(apiKey))
                        request.Headers.Add("X-Api-Key", apiKey); // TODO: confirm real auth header/scheme

                    var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return JsonConvert.DeserializeObject<DocumentManifest>(json);
                }
            }
            catch (Exception ex) when (!(ex is DownloadException))
            {
                throw new DownloadException($"Failed to fetch manifest for token '{token}': {ex.Message}", ex);
            }
        }

        public List<string> DownloadAll(DocumentManifest manifest, string destinationFolder)
        {
            var localPaths = new List<string>();

            foreach (var doc in manifest.Documents)
            {
                string url = doc.DownloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? doc.DownloadUrl
                    : $"{baseUrl.TrimEnd('/')}{doc.DownloadUrl}";

                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        if (!string.IsNullOrEmpty(apiKey))
                            request.Headers.Add("X-Api-Key", apiKey);

                        var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                        response.EnsureSuccessStatusCode();
                        byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();

                        string safeFileName = string.Join("_", (doc.FileName ?? $"{Guid.NewGuid():N}.pdf").Split(Path.GetInvalidFileNameChars()));
                        string localPath = Path.Combine(destinationFolder, safeFileName);
                        File.WriteAllBytes(localPath, bytes);
                        localPaths.Add(localPath);
                    }
                }
                catch (Exception ex) when (!(ex is DownloadException))
                {
                    throw new DownloadException($"Failed to download '{doc.FileName}' ({doc.DownloadUrl}): {ex.Message}", ex);
                }
            }

            return localPaths;
        }
    }
}
