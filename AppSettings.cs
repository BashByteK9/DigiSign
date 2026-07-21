namespace DigiSign
{
    public class AppSettings
    {
        public bool VerboseMode { get; set; } = false;
        public int Port { get; set; } = 8943;
        public string InvoiceApiBaseUrl { get; set; } = "";
        public string InvoiceApiKey { get; set; } = "";
        public bool NoAuthApi { get; set; } = false;
        public bool IncludeSignedPdfInCallback { get; set; } = true;
        public string InvoiceSignedCallbackUrl { get; set; } = "";
        public bool EnableListenerMode { get; set; } = false;
        public string PrinterName { get; set; } = "";
        public bool EnableOcspCheck { get; set; } = true;
        public int OcspTimeoutSeconds { get; set; } = 10;
    }
}
