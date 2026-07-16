using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace DigiSign
{
    public class TokenInvoice
    {
        public string TokenId { get; set; }
        public string InvoiceNo { get; set; }
    }

    public class SignRequestBatch
    {
        public string ClientId { get; set; }
        public System.Collections.Generic.List<TokenInvoice> Tokens { get; set; }
    }

    public class DownloadException : Exception
    {
        public DownloadException(string message, Exception inner = null) : base(message, inner) { }
    }

    public class CallbackException : Exception
    {
        public CallbackException(string message, Exception inner = null) : base(message, inner) { }
    }

    public interface IDocumentDownloader
    {
        /// <summary>
        /// Fetches the raw PDF bytes for a single invoice. Throws DownloadException on failure.
        /// </summary>
        byte[] FetchInvoiceDocument(string clientId, string tokenId);

        /// <summary>
        /// Posts the signed PDF back to the ERP's invoice-signed callback. Throws CallbackException on failure.
        /// </summary>
        void PostSignedInvoiceCallback(string clientId, string tokenId, string invoiceNo, byte[] signedPdfBytes);
    }

    public class HttpDocumentDownloader : IDocumentDownloader
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        private readonly string baseUrl;
        private readonly string apiKey;
        private readonly bool noAuth;

        private class InvoiceFetchRequest
        {
            public string ClientId { get; set; }
            public string TokenId { get; set; }
        }

        private class InvoiceSignedRequest
        {
            public string ClientId { get; set; }
            public string TokenId { get; set; }
            public string InvoiceNo { get; set; }

            [JsonProperty("signed-pdf")]
            public string SignedPdfBase64 { get; set; }
        }

        public HttpDocumentDownloader(string baseUrl, string apiKey, bool noAuth)
        {
            this.baseUrl = baseUrl;
            this.apiKey = apiKey;
            this.noAuth = noAuth;
        }

        public byte[] FetchInvoiceDocument(string clientId, string tokenId)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new DownloadException("Invoice/Label API base URL is not configured. Set it under the API Settings tab.");

            string url = $"{baseUrl.TrimEnd('/')}/invoice/";
            string json = JsonConvert.SerializeObject(new InvoiceFetchRequest { ClientId = clientId, TokenId = tokenId });

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    if (!noAuth && !string.IsNullOrEmpty(apiKey))
                        request.Headers.Add("X-Api-Key", apiKey); // TODO: confirm real auth header/scheme

                    var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex) when (!(ex is DownloadException))
            {
                throw new DownloadException($"Failed to fetch invoice document for token '{tokenId}': {ex.Message}", ex);
            }
        }

        public void PostSignedInvoiceCallback(string clientId, string tokenId, string invoiceNo, byte[] signedPdfBytes)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new CallbackException("Invoice/Label API base URL is not configured. Set it under the API Settings tab.");

            string url = $"{baseUrl.TrimEnd('/')}/invoice-signed/";
            string json = JsonConvert.SerializeObject(new InvoiceSignedRequest
            {
                ClientId = clientId,
                TokenId = tokenId,
                InvoiceNo = invoiceNo,
                SignedPdfBase64 = Convert.ToBase64String(signedPdfBytes)
            });

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    if (!noAuth && !string.IsNullOrEmpty(apiKey))
                        request.Headers.Add("X-Api-Key", apiKey);

                    var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex) when (!(ex is CallbackException))
            {
                throw new CallbackException($"Failed to post invoice-signed callback for token '{tokenId}': {ex.Message}", ex);
            }
        }
    }
}
