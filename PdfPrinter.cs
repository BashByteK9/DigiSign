using System;
using System.Drawing.Printing;
using System.Linq;

namespace DigiSign
{
    public interface IPrintService
    {
        /// <summary>
        /// Prints the file at pdfPath to the given printer (or the system default if printerName is blank).
        /// Throws PrintException on failure.
        /// </summary>
        void Print(string pdfPath, string printerName);
    }

    public class PrintException : Exception
    {
        public PrintException(string message, Exception inner = null) : base(message, inner) { }
    }

    public class SpirePdfPrinter : IPrintService
    {
        public void Print(string pdfPath, string printerName)
        {
            if (!string.IsNullOrWhiteSpace(printerName) &&
                !PrinterSettings.InstalledPrinters.Cast<string>().Any(p => string.Equals(p, printerName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new PrintException($"Configured printer '{printerName}' is not installed on this machine.");
            }

            try
            {
                var doc = new Spire.Pdf.PdfDocument(pdfPath);
                try
                {
                    if (!string.IsNullOrWhiteSpace(printerName))
                        doc.PrintSettings.PrinterName = printerName;

                    doc.Print();
                }
                finally
                {
                    doc.Close();
                }
            }
            catch (Exception ex) when (!(ex is PrintException))
            {
                throw new PrintException(
                    $"Failed to print '{pdfPath}'" +
                    (string.IsNullOrWhiteSpace(printerName) ? " to default printer" : $" to printer '{printerName}'") +
                    $": {ex.Message}", ex);
            }
        }
    }
}
