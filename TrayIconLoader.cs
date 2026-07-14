using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;

namespace DigiSign
{
    internal static class TrayIconLoader
    {
        public static Icon LoadFromEmbeddedPng(string resourceName)
        {
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Logger.Warning($"Tray icon resource not found: {resourceName}");
                        return SystemIcons.Application;
                    }

                    using (var source = new Bitmap(stream))
                    using (var resized = new Bitmap(32, 32))
                    {
                        using (var g = Graphics.FromImage(resized))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.DrawImage(source, 0, 0, 32, 32);
                        }

                        IntPtr hIcon = resized.GetHicon();
                        return Icon.FromHandle(hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to load tray icon from embedded resource '{resourceName}': {ex.Message}");
                return SystemIcons.Application;
            }
        }
    }
}
