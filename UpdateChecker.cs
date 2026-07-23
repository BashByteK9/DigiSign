using System;
using System.Net.Http;
using Newtonsoft.Json;

namespace DigiSign
{
    public class UpdateManifest
    {
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string Sha256 { get; set; }
        public string Notes { get; set; }
    }

    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public UpdateManifest Manifest { get; set; }
    }

    /// <summary>
    /// Checks a configurable, optional manifest URL for a newer DigiSign version. Every failure
    /// (network error, bad JSON, unparsable version) is swallowed and logged only - a broken
    /// update check must never block startup or signing, mirroring the fail-open pattern used
    /// for TSA/OCSP elsewhere in this codebase.
    /// </summary>
    public static class UpdateChecker
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>Returns null if UpdateCheckUrl is blank, the check fails, or the app is already up to date.</summary>
        public static UpdateCheckResult CheckForUpdate(string updateCheckUrl)
        {
            if (string.IsNullOrWhiteSpace(updateCheckUrl))
                return null;

            try
            {
                string json = httpClient.GetStringAsync(updateCheckUrl).GetAwaiter().GetResult();
                var manifest = JsonConvert.DeserializeObject<UpdateManifest>(json);

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.DownloadUrl))
                {
                    Logger.Warning("Update check: manifest is missing Version or DownloadUrl - ignoring");
                    return null;
                }

                if (!Version.TryParse(manifest.Version, out var remoteVersion))
                {
                    Logger.Warning($"Update check: could not parse remote version '{manifest.Version}' - ignoring");
                    return null;
                }

                if (!Version.TryParse(VersionInfo.FullVersion, out var currentVersion))
                {
                    Logger.Warning($"Update check: could not parse current version '{VersionInfo.FullVersion}' - skipping comparison");
                    return null;
                }

                bool isNewer = remoteVersion > currentVersion;
                Logger.Info(isNewer
                    ? $"Update check: version {manifest.Version} is available (current: {VersionInfo.FullVersion})"
                    : $"Update check: already up to date (current: {VersionInfo.FullVersion}, latest: {manifest.Version})");

                return new UpdateCheckResult { IsUpdateAvailable = isNewer, Manifest = manifest };
            }
            catch (Exception ex)
            {
                Logger.Warning($"Update check failed (non-fatal): {ex.Message}");
                return null;
            }
        }
    }
}
