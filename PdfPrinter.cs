using PDFtoImage;
using SkiaSharp;
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
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

    /// <summary>
    /// Renders each page via PDFtoImage (PDFium + SkiaSharp, MIT-licensed, no watermark)
    /// and prints the rendered pages through the standard GDI+ printing pipeline.
    /// </summary>
    public class PdfiumPrintService : IPrintService
    {
        private const int RenderDpi = 200;

        public void Print(string pdfPath, string printerName)
        {
            if (!string.IsNullOrWhiteSpace(printerName) &&
                !PrinterSettings.InstalledPrinters.Cast<string>().Any(p => string.Equals(p, printerName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new PrintException($"Configured printer '{printerName}' is not installed on this machine.");
            }

            try
            {
                byte[] pdfBytes = File.ReadAllBytes(pdfPath);
                int pageCount = Conversion.GetPageCount(pdfBytes);

                using (var printDoc = new PrintDocument())
                {
                    if (!string.IsNullOrWhiteSpace(printerName))
                        printDoc.PrinterSettings.PrinterName = printerName;

                    int currentPage = 0;
                    printDoc.PrintPage += (sender, e) =>
                    {
                        var options = new RenderOptions(Dpi: RenderDpi, WithAnnotations: true, WithFormFill: true, WithAspectRatio: true);
                        using (SKBitmap pageBitmap = Conversion.ToImage(pdfBytes, currentPage, password: null, options: options))
                        using (var pngStream = new MemoryStream())
                        {
                            pageBitmap.Encode(pngStream, SKEncodedImageFormat.Png, 100);
                            pngStream.Position = 0;

                            using (Image pageImage = Image.FromStream(pngStream))
                            {
                                Rectangle bounds = e.PageBounds;
                                float scale = Math.Min((float)bounds.Width / pageImage.Width, (float)bounds.Height / pageImage.Height);
                                int drawWidth = (int)(pageImage.Width * scale);
                                int drawHeight = (int)(pageImage.Height * scale);
                                e.Graphics.DrawImage(pageImage, bounds.X, bounds.Y, drawWidth, drawHeight);
                            }
                        }

                        currentPage++;
                        e.HasMorePages = currentPage < pageCount;
                    };

                    printDoc.Print();
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
