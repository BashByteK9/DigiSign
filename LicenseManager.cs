using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace DigiSign
{
    /// <summary>
    /// Helper class to hold results from license generation form
    /// </summary>
    public class LicenseGenerationResult
    {
        public string LicenseKeyPath { get; set; }
        public string CustomerId { get; set; }
        public string LicenseNumber { get; set; }
        public DateTime ExpirationDate { get; set; }
        public bool WasCancelled { get; set; }
    }

    /// <summary>
    /// Manages all licensing operations including validation, generation, and device identification
    /// </summary>
    public static class LicenseManager
    {
        #region Device Identification

        /// <summary>
        /// Gets a unique identifier for the current device based on CPU and disk serial numbers
        /// </summary>
        public static string GetDeviceId()
        {
            try
            {
                string cpuId = "";
                string diskId = "";

                var cpuSearcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (var obj in cpuSearcher.Get())
                {
                    cpuId = obj["ProcessorId"]?.ToString();
                    break;
                }

                var diskSearcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive");
                foreach (var obj in diskSearcher.Get())
                {
                    diskId = obj["SerialNumber"]?.ToString();
                    break;
                }

                return $"{cpuId}_{diskId}";
            }
            catch
            {
                return "UNKNOWN_DEVICE";
            }
        }

        /// <summary>
        /// Generates a secure hash for device validation
        /// Includes ValidUntil to prevent date tampering
        /// </summary>
        public static string GenerateDeviceHash(string deviceId, string licenseNumber, string validUntil)
        {
            // Include ValidUntil in hash to prevent date tampering
            string data = deviceId + "|" + licenseNumber + "|" + validUntil;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        #endregion

        #region User License Validation

        /// <summary>
        /// Validates a user license file
        /// </summary>
        /// <param name="filePath">Path to the license.txt file</param>
        /// <returns>True if license is valid and not expired</returns>
        public static bool ValidateLicense(string filePath)
        {
            try
            {
                Logger.Debug("Starting license validation");
                var lines = File.ReadAllLines(filePath);
                var licenseData = new Dictionary<string, string>();
                
                // Parse license file with validation
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        licenseData[parts[0].Trim()] = parts[1].Trim();
                    }
                }

                // Check all required keys exist
                if (!licenseData.ContainsKey("DeviceID"))
                {
                    Logger.Warning("License validation failed: DeviceID field is missing");
                    return false;
                }
                if (!licenseData.ContainsKey("DeviceHash"))
                {
                    Logger.Warning("License validation failed: DeviceHash field is missing");
                    return false;
                }
                if (!licenseData.ContainsKey("LicenseNumber"))
                {
                    Logger.Warning("License validation failed: LicenseNumber field is missing");
                    return false;
                }
                if (!licenseData.ContainsKey("ValidUntil"))
                {
                    Logger.Warning("License validation failed: ValidUntil field is missing");
                    return false;
                }

                string storedDeviceId = licenseData["DeviceID"];
                string storedHash = licenseData["DeviceHash"];
                string licenseNumber = licenseData["LicenseNumber"];
                string validUntil = licenseData["ValidUntil"];

                Logger.Debug($"License Number: {licenseNumber}");
                Logger.Debug($"Valid Until: {validUntil}");
                Logger.Debug($"Stored Device ID: {storedDeviceId}");

                // Validate that required values are not empty
                if (string.IsNullOrWhiteSpace(storedDeviceId))
                {
                    Logger.Warning("License validation failed: DeviceID is empty");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(storedHash))
                {
                    Logger.Warning("License validation failed: DeviceHash is empty");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(licenseNumber))
                {
                    Logger.Warning("License validation failed: LicenseNumber is empty");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(validUntil))
                {
                    Logger.Warning("License validation failed: ValidUntil is empty");
                    return false;
                }

                string currentDeviceId = GetDeviceId();
                Logger.Debug($"Current Device ID: {currentDeviceId}");

                if (storedDeviceId != currentDeviceId)
                {
                    Logger.Warning($"License validation failed: Device mismatch");
                    Logger.Warning($"  License DeviceID: {storedDeviceId}");
                    Logger.Warning($"  Current DeviceID: {currentDeviceId}");
                    Logger.Info("TIP: This license was generated for a different computer. You need to:");
                    Logger.Info("  1. Run the application on the original computer, OR");
                    Logger.Info("  2. Generate a new license.key file on this computer");
                    Logger.Info("  3. Send the new license.key to admin to generate a new license.txt");
                    return false;
                }

                // CRITICAL: Include ValidUntil in hash to prevent date tampering
                string computedHash = GenerateDeviceHash(currentDeviceId, licenseNumber, validUntil);
                Logger.Debug($"Stored Hash:   {storedHash}");
                Logger.Debug($"Computed Hash: {computedHash}");
                
                if (computedHash != storedHash)
                {
                    Logger.Warning($"License validation failed: Device hash mismatch");
                    Logger.Warning($"  Expected Hash: {storedHash}");
                    Logger.Warning($"  Computed Hash: {computedHash}");
                    Logger.Info("This usually means the license file has been tampered with.");
                    Logger.Info("Common causes:");
                    Logger.Info("  - ValidUntil date was manually changed");
                    Logger.Info("  - License file was edited or corrupted");
                    Logger.Info("  - License file is from a different device");
                    return false;
                }

                if (!DateTime.TryParse(validUntil, out var validDate))
                {
                    Logger.Warning($"License validation failed: Invalid date format: {validUntil}");
                    return false;
                }
                
                if (validDate < DateTime.Now)
                {
                    Logger.Warning($"License validation failed: License expired");
                    Logger.Warning($"  Expiry date: {validDate:yyyy-MM-dd}");
                    Logger.Warning($"  Current date: {DateTime.Now:yyyy-MM-dd}");
                    return false;
                }

                Logger.Info("✅ License validation successful");
                Logger.Info($"  Customer ID: {(licenseData.ContainsKey("CustomerID") ? licenseData["CustomerID"] : "N/A")}");
                Logger.Info($"  License Number: {licenseNumber}");
                Logger.Info($"  Valid Until: {validDate:yyyy-MM-dd}");
                
                // Check if license is expiring soon (within 15 days)
                TimeSpan timeUntilExpiry = validDate - DateTime.Now;
                int daysRemaining = (int)timeUntilExpiry.TotalDays;
                
                if (daysRemaining <= 15 && daysRemaining > 0)
                {
                    Logger.Warning($"License expiring soon: {daysRemaining} days remaining");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Error validating license", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets the number of days until license expiration
        /// </summary>
        /// <param name="filePath">Path to the license.txt file</param>
        /// <returns>Number of days remaining, or -1 if invalid/missing</returns>
        public static int GetLicenseExpiryDays(string filePath)
        {
            try
            {
                Logger.Debug($"Checking license expiry for: {filePath}");
                var lines = File.ReadAllLines(filePath);
                var licenseData = new Dictionary<string, string>();
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        licenseData[parts[0].Trim()] = parts[1].Trim();
                    }
                }

                if (licenseData.ContainsKey("ValidUntil"))
                {
                    string validUntilStr = licenseData["ValidUntil"];
                    Logger.Debug($"ValidUntil found: {validUntilStr}");
                    
                    if (DateTime.TryParse(validUntilStr, out var validDate))
                    {
                        TimeSpan timeUntilExpiry = validDate - DateTime.Now;
                        int daysRemaining = (int)timeUntilExpiry.TotalDays;
                        
                        Logger.Debug($"License expires on: {validDate:yyyy-MM-dd}");
                        Logger.Debug($"Current date: {DateTime.Now:yyyy-MM-dd}");
                        Logger.Debug($"Days remaining: {daysRemaining}");
                        
                        return daysRemaining;
                    }
                    else
                    {
                        Logger.Warning($"Failed to parse ValidUntil date: {validUntilStr}");
                    }
                }
                else
                {
                    Logger.Warning("ValidUntil field not found in license file");
                }
                
                return -1; // Invalid or missing date
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting license expiry days", ex);
                return -1;
            }
        }

        /// <summary>
        /// Shows a warning dialog if license is expiring soon
        /// </summary>
        /// <param name="daysRemaining">Number of days until expiration</param>
        public static void ShowLicenseExpirationWarning(int daysRemaining)
        {
            try
            {
                Logger.Warning($"Showing license expiration warning: {daysRemaining} days remaining");
                
                string message = $"⚠️ LICENSE EXPIRATION WARNING ⚠️\n\n" +
                                $"Your license will expire in {daysRemaining} day{(daysRemaining > 1 ? "s" : "")}.\n\n" +
                                $"Please contact your administrator to renew your license\n" +
                                $"before it expires to avoid service interruption.\n\n" +
                                $"Click OK to continue.";
                
                string title = "License Expiring Soon";
                
                MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                
                Logger.Info("License expiration warning displayed to user");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to show license expiration warning", ex);
            }
        }

        #endregion

        #region Admin License Validation

        /// <summary>
        /// Validates an admin license file
        /// </summary>
        /// <param name="adminLicensePath">Path to the admin.license file</param>
        /// <returns>True if admin license is valid</returns>
        public static bool ValidateAdminLicense(string adminLicensePath)
        {
            try
            {
                var lines = File.ReadAllLines(adminLicensePath);
                var adminData = new Dictionary<string, string>();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        adminData[parts[0].Trim()] = parts[1].Trim();
                    }
                }

                // Check required fields
                if (!adminData.ContainsKey("AdminID") || 
                    !adminData.ContainsKey("AdminKey") || 
                    !adminData.ContainsKey("ValidUntil"))
                {
                    return false;
                }

                // Validate expiration
                if (DateTime.TryParse(adminData["ValidUntil"], out var validDate) && validDate < DateTime.Now)
                {
                    Logger.Warning("Admin license has expired");
                    return false;
                }

                // Simple validation - in production, you'd validate AdminKey against AdminID
                string expectedKey = GenerateAdminKey(adminData["AdminID"]);
                if (adminData["AdminKey"] != expectedKey)
                {
                    Logger.Warning("Invalid admin license key");
                    return false;
                }

                // Only log success, not failure details
                Logger.Info("Admin license validated successfully");
                return true;
            }
            catch (Exception ex)
            {
                // Log exception but don't reveal validation logic
                Logger.Error("Admin license validation failed", ex);
                return false;
            }
        }

        /// <summary>
        /// Generates an admin key for validation
        /// </summary>
        private static string GenerateAdminKey(string adminId)
        {
            // Simple hash for admin key - in production use a more secure method
            using (SHA256 sha = SHA256.Create())
            {
                string data = adminId + "|DIGISIGN_ADMIN_SECRET";
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        #endregion

        #region License Generation

        /// <summary>
        /// Generates a license.key file for the current device
        /// </summary>
        /// <param name="licenseKeyPath">Path where the license.key file should be created</param>
        public static void GenerateLicenseKeyFile(string licenseKeyPath)
        {
            try
            {
                if (File.Exists(licenseKeyPath))
                {
                    Logger.Info($"License key file already exists at: {licenseKeyPath}");
                    return;
                }

                Logger.Info("Generating license key file");
                string deviceId = GetDeviceId();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string machineName = Environment.MachineName;
                string userName = Environment.UserName;

                Logger.Debug($"Device ID: {deviceId}");
                Logger.Debug($"Machine Name: {machineName}");
                Logger.Debug($"User Name: {userName}");

                var keyContent = new StringBuilder();
                keyContent.AppendLine("# License Key File");
                keyContent.AppendLine("# Share this file with your administrator to generate a license");
                keyContent.AppendLine();
                keyContent.AppendLine($"DeviceID={deviceId}");
                keyContent.AppendLine($"MachineName={machineName}");
                keyContent.AppendLine($"UserName={userName}");
                keyContent.AppendLine($"GeneratedOn={timestamp}");

                File.WriteAllText(licenseKeyPath, keyContent.ToString());
                Logger.Info($"License key file created successfully: {licenseKeyPath}");
            }
            catch (Exception ex)
            {
                Logger.Error("Error generating license key file", ex);
            }
        }

        /// <summary>
        /// Generates a license.txt file from form data
        /// </summary>
        /// <param name="formData">License generation form data</param>
        /// <returns>True if license was generated successfully</returns>
        public static bool GenerateLicenseFromForm(LicenseGenerationResult formData)
        {
            try
            {
                Logger.Info($"Generating license from form data");
                Logger.Info($"  License key file: {formData.LicenseKeyPath}");
                
                // Read license.key file
                var keyLines = File.ReadAllLines(formData.LicenseKeyPath);
                var keyData = new Dictionary<string, string>();

                foreach (var line in keyLines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        keyData[parts[0].Trim()] = parts[1].Trim();
                    }
                }

                if (!keyData.ContainsKey("DeviceID"))
                {
                    Logger.Error("Invalid license.key file - missing DeviceID");
                    MessageBox.Show("Invalid license.key file - missing DeviceID.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                string deviceId = keyData["DeviceID"];
                
                Logger.Debug($"Device ID from key: {deviceId}");
                Logger.Debug($"Customer ID: {formData.CustomerId}");
                Logger.Debug($"License Number: {formData.LicenseNumber}");
                Logger.Debug($"Valid Until: {formData.ExpirationDate:yyyy-MM-dd}");

                // Generate device hash - IMPORTANT: Include ValidUntil to prevent date tampering
                string validUntilStr = formData.ExpirationDate.ToString("yyyy-MM-dd");
                string deviceHash = GenerateDeviceHash(deviceId, formData.LicenseNumber, validUntilStr);
                Logger.Debug($"Generated Device Hash: {deviceHash}");
                Logger.Info("Hash includes expiration date - prevents date tampering in license file");

                // Create license.txt in the same directory as license.key
                string outputDir = Path.GetDirectoryName(formData.LicenseKeyPath);
                string licensePath = Path.Combine(outputDir, "license.txt");

                var licenseContent = new StringBuilder();
                licenseContent.AppendLine($"CustomerID={formData.CustomerId}");
                licenseContent.AppendLine($"ValidUntil={formData.ExpirationDate:yyyy-MM-dd}");
                licenseContent.AppendLine($"DeviceID={deviceId}");
                licenseContent.AppendLine($"LicenseNumber={formData.LicenseNumber}");
                licenseContent.AppendLine($"DeviceHash={deviceHash}");

                File.WriteAllText(licensePath, licenseContent.ToString());
                Logger.Info($"License file generated successfully: {licensePath}");
                Logger.Debug($"Customer ID: {formData.CustomerId}");
                Logger.Debug($"License Number: {formData.LicenseNumber}");
                Logger.Debug($"Valid Until: {formData.ExpirationDate:yyyy-MM-dd}");
                Logger.Debug($"Device ID: {deviceId}");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Error generating license from form data", ex);
                MessageBox.Show($"Error generating license: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Shows the license generation form and returns the result
        /// </summary>
        /// <returns>License generation result or null if cancelled</returns>
        public static LicenseGenerationResult ShowLicenseGenerationForm()
        {
            try
            {
                Logger.Debug("Creating LicenseGenerationForm instance");
                
                using (var form = new LicenseGenerationForm())
                {
                    Logger.Debug("Showing form dialog");
                    var dialogResult = form.ShowDialog();
                    Logger.Debug($"Dialog result: {dialogResult}");
                    
                    if (form.WasCancelled || dialogResult != DialogResult.OK)
                    {
                        Logger.Info("User cancelled the license generation form");
                        return new LicenseGenerationResult { WasCancelled = true };
                    }
                    
                    Logger.Info("User completed the license generation form");
                    return new LicenseGenerationResult
                    {
                        LicenseKeyPath = form.LicenseKeyPath,
                        CustomerId = form.CustomerId,
                        LicenseNumber = form.LicenseNumber,
                        ExpirationDate = form.ExpirationDate,
                        WasCancelled = false
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error showing license generation form", ex);
                MessageBox.Show(
                    $"Failed to show license generation form: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return new LicenseGenerationResult { WasCancelled = true };
            }
        }

        #endregion
    }
}
