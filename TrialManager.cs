using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DigiSign
{
    public class TrialStatus
    {
        public bool IsActive { get; set; }
        public int DaysRemaining { get; set; }
    }

    /// <summary>
    /// Manages the 30-day evaluation period granted on a device's first-ever run, before any
    /// purchased license.txt exists. Mirrors LicenseManager's device-hash tamper-resistance
    /// pattern (same class of protection as license.txt/admin.license - not hardened DRM, just
    /// enough to deter casual reset attempts).
    /// </summary>
    public static class TrialManager
    {
        public const string TrialFileName = "trial.lic";
        private const int TrialDurationDays = 30;

        private static string GenerateTrialHash(string deviceId, string trialStartUtc)
        {
            string data = deviceId + "|" + trialStartUtc + "|DIGISIGN_TRIAL_SECRET";
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        /// <summary>Starts the trial clock for this device the first time it's ever seen - a no-op if a trial marker already exists for the current device.</summary>
        public static void EnsureTrialStarted(string trialFilePath)
        {
            try
            {
                string currentDeviceId = LicenseManager.GetDeviceId();
                var existing = ReadTrialFile(trialFilePath);

                if (existing != null && existing.TryGetValue("DeviceID", out var storedDeviceId) && storedDeviceId == currentDeviceId)
                {
                    // Trial already started for this device - leave the original start date alone.
                    return;
                }

                if (existing != null)
                    Logger.Info("Trial marker belongs to a different device - starting a fresh trial for this device");

                string trialStartUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                string trialHash = GenerateTrialHash(currentDeviceId, trialStartUtc);

                var content = new StringBuilder();
                content.AppendLine("# DigiSign evaluation/trial marker - do not edit");
                content.AppendLine($"DeviceID={currentDeviceId}");
                content.AppendLine($"TrialStartUtc={trialStartUtc}");
                content.AppendLine($"TrialHash={trialHash}");

                File.WriteAllText(trialFilePath, content.ToString());
                Logger.Info($"Trial period started - {TrialDurationDays} days from {trialStartUtc}");
            }
            catch (Exception ex)
            {
                Logger.Error("Error ensuring trial started", ex);
            }
        }

        /// <summary>Returns whether the trial is currently active for this device, and how many days remain.</summary>
        public static TrialStatus GetTrialStatus(string trialFilePath)
        {
            try
            {
                var data = ReadTrialFile(trialFilePath);
                if (data == null)
                    return new TrialStatus { IsActive = false, DaysRemaining = 0 };

                if (!data.TryGetValue("DeviceID", out var storedDeviceId) ||
                    !data.TryGetValue("TrialStartUtc", out var trialStartUtc) ||
                    !data.TryGetValue("TrialHash", out var storedHash))
                {
                    Logger.Warning("Trial marker is missing required fields - treating as expired");
                    return new TrialStatus { IsActive = false, DaysRemaining = 0 };
                }

                string currentDeviceId = LicenseManager.GetDeviceId();
                if (storedDeviceId != currentDeviceId)
                {
                    // Marker belongs to a different device (e.g. folder copied from elsewhere) - not
                    // applicable here. EnsureTrialStarted will replace it with a fresh one for this device.
                    return new TrialStatus { IsActive = false, DaysRemaining = 0 };
                }

                string expectedHash = GenerateTrialHash(currentDeviceId, trialStartUtc);
                if (expectedHash != storedHash)
                {
                    Logger.Warning("Trial marker hash mismatch - treating trial as expired (possible tampering)");
                    return new TrialStatus { IsActive = false, DaysRemaining = 0 };
                }

                if (!DateTime.TryParse(trialStartUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var startDate))
                {
                    Logger.Warning($"Trial marker has an unparseable TrialStartUtc: {trialStartUtc}");
                    return new TrialStatus { IsActive = false, DaysRemaining = 0 };
                }

                double daysElapsed = (DateTime.UtcNow - startDate).TotalDays;
                int daysRemaining = Math.Max(0, (int)Math.Ceiling(TrialDurationDays - daysElapsed));

                return new TrialStatus
                {
                    IsActive = daysElapsed < TrialDurationDays,
                    DaysRemaining = daysRemaining
                };
            }
            catch (Exception ex)
            {
                Logger.Error("Error checking trial status", ex);
                return new TrialStatus { IsActive = false, DaysRemaining = 0 };
            }
        }

        private static Dictionary<string, string> ReadTrialFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                var result = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                        result[parts[0].Trim()] = parts[1].Trim();
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not read trial marker file: {ex.Message}");
                return null;
            }
        }
    }
}
