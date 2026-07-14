namespace DigiSign
{
    public class AppSettings
    {
        public bool VerboseMode { get; set; } = false;
        public int Port { get; set; } = 8943;
        public string InvoiceApiBaseUrl { get; set; } = "";
        public string InvoiceApiKey { get; set; } = "";
        public bool LaunchInBatchMode { get; set; } = false;
    }
}
