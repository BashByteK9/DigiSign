using System;
using System.Drawing.Printing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DigiSign
{
    /// <summary>
    /// Sends a raw command string (e.g. ZPL) straight to a printer's spooler queue using the
    /// RAW datatype, bypassing GDI+ rendering entirely - unlike IPrintService/PdfiumPrintService,
    /// which rasterizes a PDF through the standard printing pipeline.
    /// </summary>
    public interface ILabelPrintService
    {
        /// <summary>
        /// Sends rawPayload as raw bytes to printerName (or the system default printer if blank).
        /// Throws PrintException on failure, including if the spooler/printer doesn't respond
        /// within RawZplPrintService's internal timeout.
        /// </summary>
        void PrintRaw(string rawPayload, string printerName);
    }

    public class RawZplPrintService : ILabelPrintService
    {
        // A raw spooler write should return almost immediately - the printer itself streams
        // and counts copies on its own from any embedded ^PQ command. This guards against a
        // stuck spooler/offline printer hanging the synchronous HTTP request indefinitely,
        // mirroring the PerServerTimeoutMs pattern used for TSA servers in SignatureHelper.cs.
        private const int RawPrinterTimeoutMs = 15000;

        public void PrintRaw(string rawPayload, string printerName)
        {
            if (!string.IsNullOrWhiteSpace(printerName) &&
                !PrinterSettings.InstalledPrinters.Cast<string>().Any(p => string.Equals(p, printerName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new PrintException($"Configured printer '{printerName}' is not installed on this machine.");
            }

            string effectivePrinterName = string.IsNullOrWhiteSpace(printerName)
                ? new PrinterSettings().PrinterName
                : printerName;

            if (!PrinterStatusChecker.IsOnline(effectivePrinterName, out string offlineReason))
            {
                throw new PrintException($"Printer '{effectivePrinterName}' is offline or unreachable: {offlineReason}.");
            }

            try
            {
                byte[] payloadBytes = Encoding.ASCII.GetBytes(rawPayload);
                var task = Task.Run(() => RawPrinterHelper.SendBytesToPrinter(effectivePrinterName, payloadBytes));

                if (!task.Wait(RawPrinterTimeoutMs))
                {
                    throw new PrintException($"Printer '{effectivePrinterName}' did not respond within {RawPrinterTimeoutMs / 1000}s.");
                }

                task.GetAwaiter().GetResult();
            }
            catch (Exception ex) when (!(ex is PrintException))
            {
                throw new PrintException(
                    $"Failed to print label" +
                    (string.IsNullOrWhiteSpace(printerName) ? " to default printer" : $" to printer '{printerName}'") +
                    $": {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Thin wrapper around the winspool.drv RAW-datatype printing APIs (the standard approach
    /// for sending printer-native command languages like ZPL directly to the spooler).
    /// </summary>
    internal static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DOC_INFO_1
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] ref DOC_INFO_1 pDocInfo);

        [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static void SendBytesToPrinter(string printerName, byte[] bytes)
        {
            IntPtr hPrinter = IntPtr.Zero;
            IntPtr pUnmanagedBytes = IntPtr.Zero;
            var docInfo = new DOC_INFO_1
            {
                pDocName = "DigiSign Label",
                pOutputFile = null,
                pDataType = "RAW"
            };

            if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
                throw LastWin32Error($"Unable to open printer '{printerName}'");

            try
            {
                if (!StartDocPrinter(hPrinter, 1, ref docInfo))
                    throw LastWin32Error("StartDocPrinter failed");

                try
                {
                    if (!StartPagePrinter(hPrinter))
                        throw LastWin32Error("StartPagePrinter failed");

                    try
                    {
                        pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
                        Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);

                        if (!WritePrinter(hPrinter, pUnmanagedBytes, bytes.Length, out int written) || written != bytes.Length)
                            throw LastWin32Error("WritePrinter failed or wrote incomplete data");
                    }
                    finally
                    {
                        EndPagePrinter(hPrinter);
                    }
                }
                finally
                {
                    EndDocPrinter(hPrinter);
                }
            }
            finally
            {
                if (pUnmanagedBytes != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pUnmanagedBytes);
                ClosePrinter(hPrinter);
            }
        }

        private static Exception LastWin32Error(string message)
        {
            return new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), message);
        }
    }
}
