using System;
using System.Management;

namespace DigiSign
{
    /// <summary>
    /// Best-effort printer connectivity check via WMI Win32_Printer. A spooler-level
    /// "WritePrinter succeeded" result says nothing about whether the physical device is
    /// actually reachable - a printer that's installed but unplugged/offline still accepts
    /// a spooled job silently. This catches that common case before spooling into a void.
    /// Depends on the printer's port monitor/driver actually reporting bidirectional status -
    /// not all drivers do, so this reduces but doesn't eliminate false "success" reports.
    /// </summary>
    internal static class PrinterStatusChecker
    {
        // Win32_Printer.PrinterStatus: 1=Other, 2=Unknown, 3=Idle, 4=Printing, 5=Warmup, 6=StoppedPrinting, 7=Offline
        private const int WmiOfflineStatus = 7;

        public static bool IsOnline(string printerName, out string reason)
        {
            reason = null;

            if (string.IsNullOrWhiteSpace(printerName))
                return true;

            try
            {
                string escapedName = printerName.Replace("'", "''");
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT WorkOffline, PrinterStatus FROM Win32_Printer WHERE Name = '{escapedName}'"))
                {
                    foreach (ManagementBaseObject printer in searcher.Get())
                    {
                        using (printer)
                        {
                            bool workOffline = printer["WorkOffline"] is bool offlineFlag && offlineFlag;
                            if (workOffline)
                            {
                                reason = "printer is set to work offline";
                                return false;
                            }

                            if (printer["PrinterStatus"] != null && Convert.ToInt32(printer["PrinterStatus"]) == WmiOfflineStatus)
                            {
                                reason = "printer reports an offline status";
                                return false;
                            }

                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Printer connectivity check failed for '{printerName}' - proceeding without it: {ex.Message}");
                return true;
            }

            // Not found via WMI - don't block; let the actual print attempt surface any real error.
            return true;
        }
    }
}
