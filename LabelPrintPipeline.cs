using System;

namespace DigiSign
{
    public class LabelPrintRequest
    {
        public string Printer { get; set; }
        public string Zpl { get; set; }
    }

    public class LabelPrintResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Validates and prints a single raw ZPL label request - kept separate from
    /// HttpListenerService so the HTTP layer stays thin, mirroring SigningPipeline's role
    /// for the invoice sign/print flow.
    /// </summary>
    internal static class LabelPrintPipeline
    {
        /// <summary>Returns null if request is well-formed, otherwise a user-facing validation error.</summary>
        public static string Validate(LabelPrintRequest request)
        {
            string zpl = request?.Zpl?.Trim();
            if (string.IsNullOrEmpty(zpl))
                return "Zpl is required.";

            int xaIndex = zpl.IndexOf("^XA", StringComparison.OrdinalIgnoreCase);
            int xzIndex = zpl.LastIndexOf("^XZ", StringComparison.OrdinalIgnoreCase);
            if (xaIndex < 0 || xzIndex < 0 || xzIndex < xaIndex)
                return "Zpl must contain a ^XA ... ^XZ command block.";

            return null;
        }

        public static LabelPrintResult PrintLabel(LabelPrintRequest request, ILabelPrintService printService)
        {
            string validationError = Validate(request);
            if (validationError != null)
                return new LabelPrintResult { Success = false, Error = validationError };

            try
            {
                printService.PrintRaw(request.Zpl.Trim(), request.Printer ?? "");
                return new LabelPrintResult { Success = true };
            }
            catch (PrintException ex)
            {
                return new LabelPrintResult { Success = false, Error = ex.Message };
            }
        }
    }
}
