using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace DigiSign
{
    /// <summary>
    /// Downloads and applies an update package in place. Windows won't let a running EXE
    /// overwrite its own file, so this stages the new files, writes a small PowerShell helper
    /// that waits for this process to exit before copying them over the install directory, then
    /// launches that helper and lets the caller exit.
    ///
    /// The helper's copy step explicitly excludes every license/config/log file (see
    /// <see cref="ProtectedFileNames"/>) - this is what guarantees an update (or a future
    /// installer following the same list) never clobbers a customer's license, signing settings,
    /// print format settings, or trial state.
    /// </summary>
    public static class SelfUpdater
    {
        /// <summary>Files an update must never touch - license, signing config, print settings, and the trial marker. A future installer must honor the same list.</summary>
        public static readonly string[] ProtectedFileNames =
        {
            "license.txt", "license.key", "admin.license", "IP.xml", "appsettings.json", "plf.txt", TrialManager.TrialFileName
        };

        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        /// <summary>
        /// Downloads the update zip, verifies its checksum, stages it, and launches a helper that
        /// applies it after this process exits. Throws on any failure - the caller decides how to
        /// surface that (log + notify), and the current install is left untouched on failure.
        /// </summary>
        public static void DownloadAndApply(UpdateManifest manifest, string relaunchArgs)
        {
            if (string.IsNullOrWhiteSpace(manifest?.DownloadUrl))
                throw new InvalidOperationException("Update manifest is missing a DownloadUrl.");
            if (string.IsNullOrWhiteSpace(manifest.Sha256))
                throw new InvalidOperationException("Update manifest is missing a Sha256 checksum - refusing to apply an unverified update.");

            string tempZip = Path.Combine(Path.GetTempPath(), $"digisign_update_{Guid.NewGuid():N}.zip");
            string stagingDir = Path.Combine(Path.GetTempPath(), $"digisign_update_{Guid.NewGuid():N}");

            try
            {
                Logger.Info($"Downloading update from {manifest.DownloadUrl}");
                byte[] bytes = httpClient.GetByteArrayAsync(manifest.DownloadUrl).GetAwaiter().GetResult();
                File.WriteAllBytes(tempZip, bytes);

                string actualHash = ComputeSha256(tempZip);
                if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Downloaded update failed checksum verification (expected {manifest.Sha256}, got {actualHash}) - update aborted, current install left untouched.");
                }

                Logger.Info("Update checksum verified - extracting");
                Directory.CreateDirectory(stagingDir);
                ZipFile.ExtractToDirectory(tempZip, stagingDir);

                string installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                string exePath = Path.Combine(installDir, "DigiSign.exe");
                int currentPid = Process.GetCurrentProcess().Id;

                string scriptPath = WriteHelperScript(stagingDir, installDir, exePath, relaunchArgs, currentPid);

                Logger.Info("Launching update helper - this process will exit so its files can be replaced");
                var psi = new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetTempPath()
                };
                Process.Start(psi);
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* best effort */ }
            }
        }

        private static string ComputeSha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        private static string WriteHelperScript(string stagingDir, string installDir, string exePath, string relaunchArgs, int waitForPid)
        {
            string excludeFiles = string.Join(" ", Array.ConvertAll(ProtectedFileNames, f => $"\"{f}\""));
            string scriptPath = Path.Combine(Path.GetTempPath(), $"digisign_update_apply_{Guid.NewGuid():N}.ps1");

            string script =
$@"$ErrorActionPreference = 'SilentlyContinue'
try {{ Wait-Process -Id {waitForPid} -Timeout 60 }} catch {{}}
Start-Sleep -Seconds 1

robocopy ""{stagingDir}"" ""{installDir}"" /E /XF {excludeFiles} /XD logs /NFL /NDL /NJH /NJS /NC /NS

Start-Sleep -Milliseconds 500
Start-Process -FilePath ""{exePath}"" -ArgumentList ""{relaunchArgs}"" -WorkingDirectory ""{installDir}""

Remove-Item -Path ""{stagingDir}"" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
";
            File.WriteAllText(scriptPath, script, Encoding.UTF8);
            return scriptPath;
        }
    }
}
