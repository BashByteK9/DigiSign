using System;
using System.IO;
using System.Net.Http;

namespace DigiSign
{
    public enum DocumentType
    {
        Invoice,
        Label
    }

    public class DownloadException : Exception
    {
        public DownloadException(string message, Exception inner = null) : base(message, inner) { }
    }

    public interface IInvoiceDownloader
    {
        /// <summary>
        /// Downloads the source document for (documentType, token) and returns the path
        /// to a local temp PDF file. Throws DownloadException on failure.
        /// </summary>
        string DownloadDocument(DocumentType documentType, string token);
    }

    // TODO: PLACEHOLDER - the real invoice/label download API contract (endpoint shape,
    // auth scheme, response format) is not yet known. Replace the request-building logic
    // below once the real API is specified; the IInvoiceDownloader shape shouldn't need to change.
    public class PlaceholderInvoiceDownloader : IInvoiceDownloader
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private readonly string baseUrl;
        private readonly string apiKey;

        public PlaceholderInvoiceDownloader(string baseUrl, string apiKey)
        {
            this.baseUrl = baseUrl;
            this.apiKey = apiKey;
        }

        public string DownloadDocument(DocumentType documentType, string token)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new DownloadException("Invoice/Label API base URL is not configured. Set it under the API Settings tab.");

            string segment = documentType == DocumentType.Invoice ? "invoice" : "label";
            string requestUrl = $"{baseUrl.TrimEnd('/')}/{segment}/{Uri.EscapeDataString(token)}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, requestUrl))
                {
                    if (!string.IsNullOrEmpty(apiKey))
                        request.Headers.Add("X-Api-Key", apiKey); // TODO: confirm real auth header/scheme

                    var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    byte[] pdfBytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();

                    string tempFile = Path.Combine(Path.GetTempPath(), $"digisign_{segment}_{token}_{Guid.NewGuid():N}.pdf");
                    File.WriteAllBytes(tempFile, pdfBytes);
                    return tempFile;
                }
            }
            catch (Exception ex) when (!(ex is DownloadException))
            {
                throw new DownloadException($"Failed to download {segment} document for token '{token}': {ex.Message}", ex);
            }
        }
    }
}
