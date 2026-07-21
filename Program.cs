using System;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Management;
using System.Reflection;
using System.Threading;

namespace DigiSign
{
    public class XmlData
    {
        public List<string> InputFilePaths { get; set; } = new List<string>();
        public string OutputFolderPath { get; set; }
        public string CommonName { get; set; }
        public string Pin { get; set; }
        public float XCoordinate { get; set; }
        public float YCoordinate { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string OpenOutputFolder { get; set; } // Y=Open, N=Not open
        public bool UseSelfSigned { get; set; } = false;
        public string SelfSignedPath { get; set; }
        public string SelfSignedPassword { get; set; }

        public string Copy1Label { get; set; } = "Original for Buyer";
        public bool ExtraCopiesEnabled { get; set; }
        public bool PrintAllCopies { get; set; }
        public string Copy2Label { get; set; } = "Duplicate for Transporter";
        public string Copy3Label { get; set; } = "Duplicate for Supplier";
        public string Copy4Label { get; set; } = "Extra Copy";
        public float CopyLabelX { get; set; } = 380f;
        public float CopyLabelY { get; set; } = 730f;
        public float CopyLabelWidth { get; set; } = 180f;
        public float CopyLabelHeight { get; set; } = 35f;
    }

    internal class Program
    {
        // Global flag for verbose mode
        private static bool isVerboseMode = false;
        private static bool shouldAutoClose = false;
        private static VerboseProgressForm verboseForm = null;

        [STAThread]
        static void Main(string[] args)
        {
            // Initialize logger first
            Logger.Initialize();
            Logger.Info($"Application started - {VersionInfo.TitleWithVersion}");
            Logger.Info($"Version: {VersionInfo.FullVersion} | Build Date: {VersionInfo.BuildDate}");
            Logger.Debug($"Command line arguments: {string.Join(" ", args)}");

            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            string licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.txt");
            string xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP.xml");
            
            Logger.Debug($"License file path: {licensePath}");
            Logger.Debug($"XML configuration file path: {xmlFilePath}");
            
            // Declare adminLicensePath once for the entire method scope
            string adminLicensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "admin.license");

            // Check if admin mode or settings mode is requested FIRST (before verbose mode check)
            bool isAdminMode = args.Length > 0 && args[0].Equals("/admin", StringComparison.OrdinalIgnoreCase);
            bool isSettingsMode = args.Length > 0 && args[0].Equals("/settings", StringComparison.OrdinalIgnoreCase);
            bool isTrayCompanionMode = args.Length > 0 && args[0].Equals("/traycompanion", StringComparison.OrdinalIgnoreCase);

            // Read XML data (core signing fields) and appsettings.json (listener/API fields + toggles)
            var xmlData = ReadXmlData(xmlFilePath);
            var appSettings = AppSettingsLoader.Load(AppSettingsLoader.DefaultPath, xmlFilePath);

            // No args: default to batch/signing mode unless the "enable listener mode" toggle is set.
            // Explicit "/listen" always forces listener mode regardless of the toggle.
            bool isListenMode = args.Length == 0
                ? appSettings.EnableListenerMode
                : args[0].Equals("/listen", StringComparison.OrdinalIgnoreCase);

            // Only enable verbose mode if NOT in admin mode, settings mode, tray companion mode, or listen mode
            // Admin mode, settings mode, tray companion mode, and listen mode are not the batch PDF-signing flow
            if (!isAdminMode && !isSettingsMode && !isTrayCompanionMode && !isListenMode)
            {
                // Check for verbose mode from command line OR from XML settings
                bool cmdLineVerbose = args.Any(a => a.Equals("/verbose", StringComparison.OrdinalIgnoreCase));
                bool xmlVerbose = appSettings.VerboseMode;
                isVerboseMode = cmdLineVerbose || xmlVerbose;
                shouldAutoClose = isVerboseMode;

                if (isVerboseMode)
                {
                    if (cmdLineVerbose)
                        Logger.Info("Verbose mode enabled via command line");
                    if (xmlVerbose)
                        Logger.Info("Verbose mode enabled via IP.xml settings");
                    
                    // Create and show verbose progress form
                    verboseForm = new VerboseProgressForm();
                    verboseForm.Show();
                    verboseForm.AppendText("═══════════════════════════════════════════════════════════\n", Color.Gray, true);
                    verboseForm.AppendText($"{VersionInfo.TitleWithVersion} - VERBOSE MODE\n", Color.FromArgb(0, 102, 204), true);
                    verboseForm.AppendText("═══════════════════════════════════════════════════════════\n\n", Color.Gray, true);
                    
                    if (xmlVerbose)
                    {
                        verboseForm.AppendText("Verbose mode enabled from IP.xml configuration\n", Color.Green, false);
                    }
                    if (cmdLineVerbose)
                    {
                        verboseForm.AppendText("Verbose mode enabled from command line\n", Color.Green, false);
                    }
                    verboseForm.AppendText("\n", Color.Black, false);
                    
                    Application.DoEvents(); // Process form events
                }
                
                if (isVerboseMode)
                {
                    verboseForm.UpdateProgress(1, "Initializing application...");
                    verboseForm.AppendDetail($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
                    Application.DoEvents();
                }
                
                if (isVerboseMode)
                {
                    verboseForm.UpdateProgress(2, "Loading configuration...");
                    verboseForm.AppendDetail($"License file: {Path.GetFileName(licensePath)}");
                    verboseForm.AppendDetail($"Config file: {Path.GetFileName(xmlFilePath)}");
                    Application.DoEvents();
                }
            }
            else
            {
                // Admin mode, settings mode, tray companion mode, or listen mode - verbose mode is not applicable
                if (isAdminMode)
                    Logger.Info("Admin mode - verbose mode disabled");
                else if (isSettingsMode)
                    Logger.Info("Settings mode - verbose mode disabled");
                else if (isTrayCompanionMode)
                    Logger.Info("Tray companion mode - verbose mode disabled");
                else
                    Logger.Info("Listen mode - verbose mode disabled");

                isVerboseMode = false;
                shouldAutoClose = false;
            }
            
            int totalErrorCount = 0; // Track ALL errors (validation, signing, etc.) for auto-close timing
            bool hasSuccessfulSigning = false; // Track if any PDFs were successfully signed
            string plfSuccessMessage = ""; // Message for PLF log

            // Handle settings mode (no admin license required)
            if (isSettingsMode)
            {
                // SETTINGS MODE: No license required - just show settings panel
                Logger.Info("Settings mode requested - showing settings panel (no license required)");

                // Show settings panel without license requirement. Empty relaunch args means
                // "no other mode was running" - a restart just relaunches with no args.
                if (RunSettingsMode())
                {
                    Logger.Info("Restart requested from standalone Settings - relaunching");
                    try
                    {
                        Process.Start(Application.ExecutablePath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to relaunch DigiSign after settings restart", ex);
                    }
                }
                return; // Exit after settings mode completes
            }

            if (isAdminMode)
            {
                // ADMIN MODE: Only requires admin license, not user license
                Logger.Info("Admin mode requested - checking for admin license");

                if (!File.Exists(adminLicensePath))
                {
                    Logger.Error($"Admin license not found at: {adminLicensePath}");
                    MessageBox.Show(
                        $"Admin license not found!\n\nPlease ensure 'admin.license' file exists in:\n{AppDomain.CurrentDomain.BaseDirectory}\n\nContact support for an admin license if you don't have one.",
                        "Admin License Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                if (!LicenseManager.ValidateAdminLicense(adminLicensePath))
                {
                    Logger.Error("Invalid admin license");
                    MessageBox.Show(
                        "Invalid admin license!\n\nThe admin.license file is invalid or corrupted.\n\nContact support for a valid admin license.",
                        "Invalid Admin License",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                // Admin license is valid, proceed with admin mode
                Logger.Info("Valid admin license detected - proceeding with license generation");

                RunAdminMode(adminLicensePath);
                return; // Exit after admin mode completes
            }

            // TRAY COMPANION MODE: idle background tray icon only (self-spawned by batch mode
            // when nothing else already owns the tray-presence slot) - no license required.
            if (isTrayCompanionMode)
            {
                Logger.Info("Tray companion mode requested - showing idle tray icon only (no license required)");
                RunTrayCompanionMode();
                return;
            }

            // LISTENER MODE: Tray/HTTP listener must run regardless of license validity -
            // license is checked per-request inside the listener instead of gating startup.
            if (isListenMode)
            {
                RunListenMode(xmlData, appSettings, licensePath);
                return;
            }

            // PDF SIGNING MODE: Requires user license (license.txt)
            JobRecoveryService.RunStartupRecovery();
            JobStore.Prune(TimeSpan.FromDays(30));

            // Ensure a tray icon is present in the background even though this batch run itself
            // will exit as soon as signing completes - spawn an idle companion process unless a
            // listener or an existing companion already owns the tray-presence slot.
            if (!TraySingleton.IsHeld())
            {
                try
                {
                    var companionStartInfo = new ProcessStartInfo(Application.ExecutablePath, "/traycompanion")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(companionStartInfo);
                    Logger.Info("Spawned tray companion process for background tray icon presence");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not spawn tray companion process: {ex.Message}");
                }
            }

            Logger.LogSeparator();
            Logger.Info("Starting license validation for PDF signing");

            if (isVerboseMode)
            {
                verboseForm.UpdateProgress(3, "Validating license...");
                Application.DoEvents();
            }

            // Check if admin license exists (for hint message later)
            bool hasAdminLicense = File.Exists(adminLicensePath) && LicenseManager.ValidateAdminLicense(adminLicensePath);
            
            // Check user license for PDF signing
            bool hasValidUserLicense = false;
            int licenseExpiryDaysRemaining = -1; // Track days until expiry
            
            if (File.Exists(licensePath))
            {
                Logger.Info($"License file found at: {licensePath}");
                if (LicenseManager.ValidateLicense(licensePath))
                {
                    hasValidUserLicense = true;

                    // Get days remaining for expiry check
                    licenseExpiryDaysRemaining = LicenseManager.GetLicenseExpiryDays(licensePath);

                    Logger.Info("License validation successful - Full Mode enabled");
                    if (isVerboseMode)
                    {
                        verboseForm.AppendSuccess("LICENSED - Full cryptographic signing enabled");
                        Application.DoEvents();
                    }
                }
                else
                {
                    Logger.Error("License validation failed - Cannot sign PDFs");
                    totalErrorCount++; // Count as error - PDF signing will fail
                    if (isVerboseMode)
                    {
                        verboseForm.AppendError("LICENSE INVALID - PDF signing not allowed");
                        Application.DoEvents();
                    }
                }
            }
            else
            {
                Logger.Error("License file not found - Cannot sign PDFs");
                totalErrorCount++; // Count as error - PDF signing will fail
                if (isVerboseMode)
                {
                    verboseForm.AppendError("LICENSE MISSING - PDF signing not allowed");
                    Application.DoEvents();
                }
            }
            
            // Exit if no valid user license (PDF signing requires valid user license)
            if (!hasValidUserLicense)
            {
                // Generate license.key file if it doesn't exist
                string licenseKeyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.key");
                if (!File.Exists(licenseKeyPath))
                {
                    Logger.Info("Generating license.key file for user to send to admin");
                    LicenseManager.GenerateLicenseKeyFile(licenseKeyPath);
                }
                else
                {
                    Logger.Info("license.key file already exists");
                }

                if (hasAdminLicense)
                {
                    Logger.Info("Admin license detected but not running in admin mode");
                }

                Logger.Error("Application terminated - No valid user license");
                Logger.LogToPlf("Error: No valid user license - PDF signing aborted", isError: true);

                if (isVerboseMode)
                {
                    verboseForm.AppendText("\n", Color.Black);
                    verboseForm.AppendText("═══════════════════════════════════════════════════════════\n", Color.Red, true);
                    verboseForm.AppendText("ERROR: Valid user license required for PDF signing!\n", Color.Red, true);
                    verboseForm.AppendText("═══════════════════════════════════════════════════════════\n", Color.Red, true);
                    verboseForm.AppendError("Application cannot continue without valid user license");
                    Application.DoEvents();

                    // Complete with error
                    verboseForm.ProcessingComplete(true, totalErrorCount);
                    Application.Run(verboseForm);
                }
                else
                {
                    MessageBox.Show(
                        "Valid user license required for PDF signing!\n\nPlease ensure you have a valid license.txt file.\nContact support for a license if you don't have one.",
                        "License Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                return;
            }




            Logger.Info("Application mode: FULL (valid user license)");

            // Show license expiration warning if expiring within 15 days
            if (licenseExpiryDaysRemaining >= 0 && licenseExpiryDaysRemaining <= 15)
            {
                Logger.Info($"License expires in {licenseExpiryDaysRemaining} days - showing warning dialog");
                LicenseManager.ShowLicenseExpirationWarning(licenseExpiryDaysRemaining);
            }
            else if (licenseExpiryDaysRemaining > 15)
            {
                Logger.Debug($"License has {licenseExpiryDaysRemaining} days remaining - no warning needed");
            }

            Logger.LogSeparator();

            // Show hint if user has admin license but didn't use /admin flag
            if (File.Exists(adminLicensePath) && LicenseManager.ValidateAdminLicense(adminLicensePath))
            {
                Logger.Info("Admin license detected but not running in admin mode");
            }

            Logger.LogSeparator();
            Logger.Info("Starting PDF processing");

            if (isVerboseMode)
            {
                verboseForm.UpdateProgress(4, "Validating XML configuration...");
                Application.DoEvents();
            }
            
            if (xmlData != null &&
                xmlData.InputFilePaths.Any() &&
                !string.IsNullOrEmpty(xmlData.OutputFolderPath) &&
                !string.IsNullOrEmpty(xmlData.CommonName))
            {
                Logger.Info("XML configuration validated successfully");
                Logger.Debug($"Input files count: {xmlData.InputFilePaths.Count}");
                Logger.Debug($"Output folder: {xmlData.OutputFolderPath}");
                Logger.Debug($"Certificate CN: {xmlData.CommonName}");
                
                if (isVerboseMode)
                {
                    verboseForm.AppendSuccess("Configuration valid");
                    verboseForm.AppendDetail($"Input files: {xmlData.InputFilePaths.Count}");
                    verboseForm.AppendDetail($"Output folder: {xmlData.OutputFolderPath}");
                    verboseForm.AppendDetail($"Certificate CN: {xmlData.CommonName}");
                    Application.DoEvents();
                }
                
                string outputFolderPath = xmlData.OutputFolderPath;
                string commonName = xmlData.CommonName;
                string pin = xmlData.Pin;
                float xCoord = xmlData.XCoordinate;
                float yCoord = xmlData.YCoordinate;
                float width = xmlData.Width;
                float height = xmlData.Height;
                string openOutputFolder = xmlData.OpenOutputFolder ?? "Y"; // Default to Yes

                Logger.Debug($"Signature coordinates: X={xCoord}, Y={yCoord}, Width={width}, Height={height}");

                if (isVerboseMode)
                {
                    verboseForm.UpdateProgress(5, "Creating output directory...");
                    Application.DoEvents();
                }

                // Ensure output folder exists
                if (!Directory.Exists(outputFolderPath))
                {
                    Logger.Info($"Creating output folder: {outputFolderPath}");
                    Directory.CreateDirectory(outputFolderPath);
                    if (isVerboseMode)
                    {
                        VerboseLog($"Created: {outputFolderPath}", VerboseLogType.Success);
                    }
                }
                else if (isVerboseMode)
                {
                    VerboseLog($"Already exists: {outputFolderPath}", VerboseLogType.Success);
                }

                // Filter valid PDF files
                var validPdfFiles = xmlData.InputFilePaths
                    .Where(file => File.Exists(file) && Path.GetExtension(file).ToLower() == ".pdf")
                    .ToList();

                Logger.Info($"Valid PDF files found: {validPdfFiles.Count} out of {xmlData.InputFilePaths.Count}");
                
                if (isVerboseMode)
                {
                    verboseForm.UpdateProgress(6, "Filtering PDF files...");
                    VerboseLog($"Found {validPdfFiles.Count} valid PDF(s)", VerboseLogType.Success);
                    Application.DoEvents();
                }

                if (validPdfFiles.Any())
                {
                    if (isVerboseMode)
                    {
                        verboseForm.UpdateProgress(7, "Loading certificate...");
                        VerboseLog($"Searching for: {commonName}", VerboseLogType.Detail);
                        Application.DoEvents();
                    }

                    // Create signature configuration
                    var signatureConfig = new SignatureConfiguration(xCoord, yCoord, width, height)
                    {
                        Copy1Label = xmlData.Copy1Label,
                        ExtraCopiesEnabled = xmlData.ExtraCopiesEnabled,
                        PrintAllCopies = xmlData.PrintAllCopies,
                        Copy2Label = xmlData.Copy2Label,
                        Copy3Label = xmlData.Copy3Label,
                        Copy4Label = xmlData.Copy4Label,
                        CopyLabelX = xmlData.CopyLabelX,
                        CopyLabelY = xmlData.CopyLabelY,
                        CopyLabelWidth = xmlData.CopyLabelWidth,
                        CopyLabelHeight = xmlData.CopyLabelHeight,
                        EnableOcspCheck = appSettings.EnableOcspCheck,
                        OcspTimeoutSeconds = appSettings.OcspTimeoutSeconds
                    };
                    // Track each file as its own job (Source=Batch) so a crash mid-batch leaves resumable,
                    // per-file checkpoints - the same shared model listener-mode jobs use.
                    var jobIds = validPdfFiles
                        .Select(file => JobTracker.CreateJob(
                            token: null, route: null, clientId: null,
                            invoiceNo: Path.GetFileNameWithoutExtension(file),
                            source: JobSource.Batch, inputPath: file, doSign: true, doPrint: false).JobId)
                        .ToList();

                    IBatchSignProgress progress = new CompositeBatchSignProgress(
                        isVerboseMode ? new VerboseBatchSignProgress(verboseForm) : null,
                        new BatchModeJobTrackingProgress(jobIds));

                    var result = BatchSigner.SignFiles(validPdfFiles, outputFolderPath, commonName, pin, signatureConfig, xmlData, progress);

                    if (result.CertificateError != null)
                    {
                        Logger.Error(result.CertificateError);
                        Logger.LogToPlf(result.CertificateError, isError: true);
                        totalErrorCount++; // Count certificate not found error

                        foreach (var jobId in jobIds)
                            JobTracker.Complete(jobId, false, null, result.CertificateError);
                    }
                    else
                    {
                        for (int i = 0; i < result.FileResults.Count && i < jobIds.Count; i++)
                        {
                            var fileResult = result.FileResults[i];
                            JobTracker.Complete(jobIds[i], fileResult.Success,
                                fileResult.Success ? fileResult.OutputPath : null,
                                fileResult.Success ? null : fileResult.Error);
                        }

                        // Store success info for PLF logging later (after verbose dialog closes)
                        hasSuccessfulSigning = result.SuccessCount > 0;
                        if (result.SuccessCount >= 1)
                        {
                            var firstSuccess = result.FileResults.First(r => r.Success);
                            plfSuccessMessage = $"File(s) Signed Successfully - {firstSuccess.FileName}";
                        }

                        // Add PDF signing failures to total error count
                        totalErrorCount += result.FailCount;

                        // Open output folder if specified
                        if (openOutputFolder.Equals("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                Logger.Debug($"Opening output folder: {outputFolderPath}");

                                if (isVerboseMode)
                                {
                                    verboseForm.UpdateProgress(10, "Opening output folder...");
                                    VerboseLog(outputFolderPath, VerboseLogType.Detail);
                                    Application.DoEvents();
                                }

                                Process.Start("explorer.exe", outputFolderPath);
                                Logger.Info("Output folder opened successfully");

                                if (isVerboseMode)
                                {
                                    VerboseLog("Folder opened", VerboseLogType.Success);
                                    Application.DoEvents();
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Error opening output folder: {outputFolderPath}", ex);
                                if (isVerboseMode)
                                {
                                    VerboseLog($"Could not open folder: {ex.Message}", VerboseLogType.Warning);
                                    Application.DoEvents();
                                }
                            }
                        }
                        else if (isVerboseMode)
                        {
                            verboseForm.UpdateProgress(10, "Skipping folder open (OpenOutputFolder=N)");
                            Application.DoEvents();
                        }
                    }
                }
                else
                {
                    Logger.Error("No valid PDF files found");
                    Logger.LogToPlf("Error: No valid PDF files found", isError: true);
                    totalErrorCount++; // Count no valid PDFs error
                    
                    if (isVerboseMode)
                    {
                        VerboseLog("No valid PDF files found", VerboseLogType.Error);
                        Application.DoEvents();
                    }
                }
            }
            else
            {
                Logger.Error($"Invalid XML configuration in: {xmlFilePath}");
                if (xmlData == null)
                    Logger.Error("XML data is null - file may be missing or corrupted");
                else
                {
                    if (!xmlData.InputFilePaths.Any())
                        Logger.Error("No input file paths specified");
                    if (string.IsNullOrEmpty(xmlData.OutputFolderPath))
                        Logger.Error("Output folder path is missing");
                    if (string.IsNullOrEmpty(xmlData.CommonName))
                        Logger.Error("Certificate common name is missing");
                }
                Logger.LogToPlf($"Error: Invalid XML data in {xmlFilePath}", isError: true);
                totalErrorCount++; // Count XML validation error
                
                if (isVerboseMode)
                {
                    VerboseLog("Invalid XML configuration", VerboseLogType.Error);
                    Application.DoEvents();
                }
            }
            
            Logger.LogSeparator();
            Logger.Info("Application completed");
            
            if (isVerboseMode)
            {
                verboseForm.AppendText("\n", Color.Black);
                verboseForm.AppendText("═══════════════════════════════════════════════════════════\n", Color.Gray, true);
                verboseForm.AppendText("Application completed.\n", Color.Black, true);
                verboseForm.AppendText("═══════════════════════════════════════════════════════════\n", Color.Gray, true);
                Application.DoEvents();
            }
            
            // Auto-close in verbose mode
            if (shouldAutoClose && isVerboseMode)
            {
                Logger.Info($"Auto-closing (verbose mode) - Total errors: {totalErrorCount}");
                verboseForm.ProcessingComplete(true, totalErrorCount); // Pass total error count for smart timing
                Application.Run(verboseForm); // Keep form alive until auto-close
                
                // Write PLF log AFTER verbose dialog has closed
                if (hasSuccessfulSigning && totalErrorCount == 0)
                {
                    Logger.LogToPlf(plfSuccessMessage, isError: false);
                    Logger.Info("PLF success log written after verbose dialog closed");
                }
                else if (hasSuccessfulSigning && totalErrorCount > 0)
                {
                    // Partial success - some files signed, some failed
                    Logger.LogToPlf($"{plfSuccessMessage} (with {totalErrorCount} error(s))", isError: false);
                    Logger.Info("PLF partial success log written after verbose dialog closed");
                }
                return;
            }
            else if (!isVerboseMode)
            {
                // Non-verbose mode - write PLF log immediately
                if (hasSuccessfulSigning && totalErrorCount == 0)
                {
                    Logger.LogToPlf(plfSuccessMessage, isError: false);
                    Logger.Info("PLF success log written (non-verbose mode)");
                }
                else if (hasSuccessfulSigning && totalErrorCount > 0)
                {
                    // Partial success
                    Logger.LogToPlf($"{plfSuccessMessage} (with {totalErrorCount} error(s))", isError: false);
                    Logger.Info("PLF partial success log written (non-verbose mode)");
                }
            }
        }

        // Helper method for verbose logging
        private static void VerboseLog(string message, VerboseLogType type = VerboseLogType.Info)
        {
            if (!isVerboseMode || verboseForm == null) return;

            switch (type)
            {
                case VerboseLogType.Success:
                    verboseForm.AppendSuccess(message);
                    break;
                case VerboseLogType.Error:
                    verboseForm.AppendError(message);
                    break;
                case VerboseLogType.Warning:
                    verboseForm.AppendWarning(message);
                    break;
                case VerboseLogType.Detail:
                    verboseForm.AppendDetail(message);
                    break;
                default:
                    verboseForm.AppendInfo(message);
                    break;
            }
            Application.DoEvents();
        }

        private enum VerboseLogType
        {
            Info,
            Success,
            Error,
            Warning,
            Detail
        }

        static void RunAdminMode(string adminLicensePath)
        {
            // Admin license is valid, proceed with license generation
            Logger.Info("Admin license validated - entering license generation mode");

            // Use Windows Forms GUI for admin mode
            Logger.Debug("Showing License Generation Form");

            LicenseGenerationResult result = LicenseManager.ShowLicenseGenerationForm();

            if (result == null || result.WasCancelled)
            {
                Logger.Info("Admin mode exited - User cancelled the license generation form");
                return;
            }
            
            Logger.Debug($"User input received from form");
            Logger.Debug($"  License Key Path: {result.LicenseKeyPath}");
            Logger.Debug($"  Customer ID: {result.CustomerId}");
            Logger.Debug($"  License Number: {result.LicenseNumber}");
            Logger.Debug($"  Expiration Date: {result.ExpirationDate:yyyy-MM-dd}");

            if (LicenseManager.GenerateLicenseFromForm(result))
            {
                Logger.Info($"License file generated successfully from: {result.LicenseKeyPath}");
                MessageBox.Show(
                    "LICENSE GENERATED SUCCESSFULLY!\n\nThe license.txt file has been created in the same folder as the license.key file.\n\nPlease send this file to the user.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                Logger.Error("Failed to generate license file");
                MessageBox.Show(
                    "Failed to generate license file.\n\nPlease check:\n  1. The license.key file is valid\n  2. You have write permissions to the output folder\n  3. The application_log.txt for detailed error information",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            // Exit after admin operations - DO NOT process PDFs
            Logger.Info("Exiting admin mode (no PDF processing)");
            Logger.Info("Application ended");
        }

        static void RunListenMode(XmlData xmlData, AppSettings appSettings, string licensePath)
        {
            Logger.LogSeparator();
            Logger.Info("Listen mode requested - starting HTTP listener tray application");

            JobRecoveryService.RunStartupRecovery();
            JobStore.Prune(TimeSpan.FromDays(30));

            if (xmlData == null || string.IsNullOrEmpty(xmlData.CommonName) || string.IsNullOrEmpty(xmlData.OutputFolderPath))
            {
                Logger.Error("Cannot start listen mode - IP.xml configuration invalid/missing (CommonName/OutputFolderPath required)");
                MessageBox.Show(
                    "Cannot start the listener: PDF signing settings are not configured.\n\nRun 'DigiSign.exe /settings' first.",
                    "DigiSign Listener",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            int port = appSettings.Port > 0 ? appSettings.Port : 8943;
            var downloader = new HttpDocumentDownloader(appSettings.InvoiceApiBaseUrl, appSettings.InvoiceApiKey, appSettings.NoAuthApi, appSettings.IncludeSignedPdfInCallback, appSettings.InvoiceSignedCallbackUrl);

            // If an idle tray companion (spawned by a prior batch run) already owns the tray-presence
            // slot, ask it to exit so the listener's own tray icon becomes the only one visible.
            if (TraySingleton.IsHeld())
            {
                Logger.Info("A tray companion is already running - requesting it exit so the listener can take over the tray icon");
                TraySingleton.RequestOtherInstanceExit();
                for (int i = 0; i < 20 && TraySingleton.IsHeld(); i++)
                    Thread.Sleep(100);
            }
            bool traySlotAcquired = TraySingleton.TryAcquire();
            if (!traySlotAcquired)
                Logger.Warning("Could not acquire the tray-presence slot - a duplicate tray icon may briefly be visible");

            using (var hostForm = new TrayHostForm())
            {
                var listenerService = new HttpListenerService(port, xmlData, downloader, licensePath,
                    issue => hostForm.BeginInvoke(new Action(() =>
                        MessageBox.Show(issue, "DigiSign Listener", MessageBoxButtons.OK, MessageBoxIcon.Warning))),
                    new PdfiumPrintService(), appSettings.PrinterName,
                    appSettings.EnableOcspCheck, appSettings.OcspTimeoutSeconds);

                JobTracker.RegisterResumeHandler(JobSource.Listener, jobId => listenerService.ProcessResumedJob(jobId));
                JobTracker.RegisterResumeHandler(JobSource.Batch, ResumeBatchJob);

                try
                {
                    listenerService.Start();
                }
                catch (Exception ex)
                {
                    Logger.Critical($"Failed to start HTTP listener on port {port}", ex);
                    MessageBox.Show(
                        $"Failed to start listener on port {port}:\n{ex.Message}",
                        "DigiSign Listener",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    if (traySlotAcquired)
                        TraySingleton.Release();
                    return;
                }

                JobsForm jobsForm = null;
                Action showJobsForm = () =>
                {
                    if (jobsForm == null || jobsForm.IsDisposed)
                    {
                        jobsForm = new JobsForm();
                        jobsForm.Show();
                    }
                    else
                    {
                        jobsForm.Show();
                        if (jobsForm.WindowState == FormWindowState.Minimized)
                            jobsForm.WindowState = FormWindowState.Normal;
                        jobsForm.BringToFront();
                        jobsForm.Activate();
                    }
                };

                var menu = new ContextMenuStrip();
                menu.Items.Add($"DigiSign Listener — port {port}").Enabled = false;
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("View Job Status...", null, (s, e) => showJobsForm());
                menu.Items.Add("Settings...", null, (s, e) =>
                {
                    if (RunSettingsMode())
                    {
                        Logger.Info("Restart requested from Settings - relaunching into listener mode");
                        // Stop the listener before spawning the new process, so the port is free
                        // before the relaunch tries to bind it (tray slot is released once via the
                        // ApplicationExit handler below, when Application.Exit() unwinds Application.Run).
                        listenerService.Stop();
                        try
                        {
                            Process.Start(Application.ExecutablePath, "/listen");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to relaunch DigiSign after settings restart", ex);
                        }
                        Application.Exit();
                    }
                });
                menu.Items.Add("Open Logs Folder", null, (s, e) =>
                {
                    try { Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs")); }
                    catch (Exception ex) { Logger.Warning($"Could not open logs folder: {ex.Message}"); }
                });
                menu.Items.Add("Exit", null, (s, e) => Application.Exit());

                using (var trayIcon = BuildTrayIcon(menu, $"DigiSign Listener (port {port})"))
                {
                    trayIcon.MouseClick += (s, e) =>
                    {
                        if (e.Button != MouseButtons.Left) return;
                        showJobsForm();
                    };

                    Application.ApplicationExit += (s, e) =>
                    {
                        listenerService.Stop();
                        if (traySlotAcquired)
                            TraySingleton.Release();
                    };

                    Logger.Info($"Listener running on http://localhost:{port}/ - waiting for requests");
                    Application.Run(hostForm);
                }

                listenerService.Dispose();
            }

            Logger.Info("Listen mode exited");
        }

        /// <summary>
        /// Resume handler for JobSource.Batch jobs (JobTracker.RegisterResumeHandler) - serviced by
        /// whichever tray/listener process happens to be alive at click-time, since the original batch
        /// CLI invocation that created the job has already exited (its fire-and-forget contract with the
        /// ERP caller is unaffected). Batch jobs never print/callback, so "signed" is their terminal state.
        /// </summary>
        private static void ResumeBatchJob(string jobId)
        {
            var job = JobTracker.GetJob(jobId);
            if (job == null)
            {
                Logger.Error($"[batch job={jobId}] Job record not found - aborting resume");
                return;
            }

            var currentProcess = Process.GetCurrentProcess();
            JobTracker.SetOwner(jobId, currentProcess.Id, currentProcess.StartTime.ToUniversalTime());

            if (job.CancellationRequested)
            {
                Logger.Info($"[batch job={jobId}] Cancelled before resume processing began");
                JobTracker.Cancel(jobId);
                return;
            }

            bool alreadySigned = job.PathsToPrint != null && job.PathsToPrint.Count > 0 && job.PathsToPrint.All(File.Exists);
            if (alreadySigned)
            {
                Logger.Info($"[batch job={jobId}] Resuming - already signed, marking complete");
                JobTracker.Complete(jobId, true, job.PathsToPrint[0], null);
                return;
            }

            if (string.IsNullOrEmpty(job.InputPath) || !File.Exists(job.InputPath))
            {
                Logger.Error($"[batch job={jobId}] Cannot resume - input file no longer exists: {job.InputPath}");
                JobTracker.Complete(jobId, false, null, $"Input file no longer exists: {job.InputPath}");
                return;
            }

            string xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP.xml");
            string licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.txt");
            var xmlData = ReadXmlData(xmlFilePath);
            var appSettings = AppSettingsLoader.Load(AppSettingsLoader.DefaultPath, xmlFilePath);

            if (xmlData == null)
            {
                Logger.Error($"[batch job={jobId}] Cannot resume - IP.xml configuration invalid/missing");
                JobTracker.Complete(jobId, false, null, "Cannot resume: IP.xml configuration invalid/missing");
                return;
            }

            var signResult = SigningPipeline.SignSingleFile(
                job.InputPath, xmlData, appSettings.EnableOcspCheck, appSettings.OcspTimeoutSeconds, licensePath,
                out string licenseIssue, new BatchModeJobTrackingProgress(new List<string> { jobId }));

            if (licenseIssue != null)
            {
                Logger.Error($"[batch job={jobId}] {licenseIssue}");
                JobTracker.Complete(jobId, false, null, licenseIssue);
                return;
            }

            if (!signResult.Success)
            {
                JobTracker.Complete(jobId, false, null, signResult.Error);
                return;
            }

            JobTracker.SetSigned(jobId, wasSigned: true, signResult.OutputPaths, null);
            JobTracker.Complete(jobId, true, signResult.OutputPaths[0], null);
        }

        /// <summary>Builds a visible <see cref="NotifyIcon"/> using DigiSign's embedded tray icon image.</summary>
        private static NotifyIcon BuildTrayIcon(ContextMenuStrip menu, string tooltip)
        {
            return new NotifyIcon
            {
                Icon = TrayIconLoader.LoadFromEmbeddedPng("DigiSign.singer_icon.png"),
                Text = tooltip,
                ContextMenuStrip = menu,
                Visible = true
            };
        }

        /// <summary>
        /// Idle background tray-icon-only mode, self-spawned by batch-signing runs so a tray icon is
        /// always present even though batch mode itself exits as soon as signing completes. Exits
        /// immediately if something else (listener, or another companion) already owns the tray slot.
        /// </summary>
        static void RunTrayCompanionMode()
        {
            Logger.LogSeparator();
            Logger.Info("Tray companion mode started - idle background tray icon only");

            JobTracker.RegisterResumeHandler(JobSource.Batch, ResumeBatchJob);

            JobRecoveryService.RunStartupRecovery();
            JobStore.Prune(TimeSpan.FromDays(30));

            if (!TraySingleton.TryAcquire())
            {
                Logger.Info("Tray companion exiting immediately - another tray icon is already running");
                return;
            }

            using (var hostForm = new TrayHostForm())
            {
                JobsForm jobsForm = null;
                Action showJobsForm = () =>
                {
                    if (jobsForm == null || jobsForm.IsDisposed)
                    {
                        jobsForm = new JobsForm();
                        jobsForm.Show();
                    }
                    else
                    {
                        jobsForm.Show();
                        if (jobsForm.WindowState == FormWindowState.Minimized)
                            jobsForm.WindowState = FormWindowState.Normal;
                        jobsForm.BringToFront();
                        jobsForm.Activate();
                    }
                };

                var menu = new ContextMenuStrip();
                menu.Items.Add("DigiSign — idle (signing mode)").Enabled = false;
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("View Job Status...", null, (s, e) => showJobsForm());
                menu.Items.Add("Settings...", null, (s, e) =>
                {
                    if (RunSettingsMode())
                    {
                        Logger.Info("Restart requested from Settings - relaunching into tray companion mode");
                        try
                        {
                            Process.Start(Application.ExecutablePath, "/traycompanion");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to relaunch DigiSign after settings restart", ex);
                        }
                        Application.Exit();
                    }
                });
                menu.Items.Add("Open Logs Folder", null, (s, e) =>
                {
                    try { Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs")); }
                    catch (Exception ex) { Logger.Warning($"Could not open logs folder: {ex.Message}"); }
                });
                menu.Items.Add("Exit", null, (s, e) => Application.Exit());

                using (var trayIcon = BuildTrayIcon(menu, "DigiSign (signing mode)"))
                {
                    trayIcon.MouseClick += (s, e) =>
                    {
                        if (e.Button != MouseButtons.Left) return;
                        showJobsForm();
                    };

                    Application.ApplicationExit += (s, e) => TraySingleton.Release();

                    TraySingleton.WatchForExitRequest(() =>
                        hostForm.BeginInvoke(new Action(() => Application.Exit())));

                    Logger.Info("Tray companion running in background - waiting");
                    Application.Run(hostForm);
                }
            }

            Logger.Info("Tray companion exited");
        }

        private static volatile bool settingsFormOpen = false;

        /// <summary>Shows the Settings form. Returns true if the user clicked "Restart App" (caller relaunches into the mode it knows was previously running).</summary>
        static bool RunSettingsMode()
        {
            if (settingsFormOpen)
            {
                Logger.Debug("Settings form already open - ignoring duplicate request");
                return false;
            }

            // Settings mode - no license required
            Logger.Info("Entering settings mode - no license required");
            settingsFormOpen = true;
            bool restartRequested = false;

            try
            {
                Logger.Debug("Creating LicenseGenerationForm for settings only");

                using (var form = new LicenseGenerationForm(settingsOnly: true))
                {
                    Logger.Debug("Showing settings form");
                    form.ShowDialog();
                    Logger.Debug("Settings form closed");
                    restartRequested = form.RestartRequested;
                }

                Logger.Info("Settings mode completed");
            }
            catch (Exception ex)
            {
                Logger.Error("Error showing settings panel", ex);
                MessageBox.Show(
                    $"Failed to show settings panel: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                settingsFormOpen = false;
            }

            return restartRequested;
        }

        static string PromptForLicenseKeyPath()
        {
            try
            {
                Logger.Debug("Showing GUI dialog for license key path");
                
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = "Select license.key File";
                    dialog.Filter = "License Key Files (*.key)|*.key|All Files (*.*)|*.*";
                    dialog.DefaultExt = "key";
                    dialog.FileName = "license.key";
                    dialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    dialog.CheckFileExists = true;
                    dialog.CheckPathExists = true;
                    
                    Logger.Debug("OpenFileDialog configured, showing to user");
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        Logger.Info($"User selected file: {dialog.FileName}");
                        return dialog.FileName;
                    }
                    else
                    {
                        Logger.Info("User cancelled file selection dialog");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error showing file selection dialog", ex);
                return null;
            }
        }

        static XmlData ReadXmlData(string xmlFilePath)
        {
            try
            {
                Logger.Debug($"Reading XML configuration from: {xmlFilePath}");
                
                if (!File.Exists(xmlFilePath))
                {
                    Logger.Error($"XML file not found: {xmlFilePath}");
                    return null;
                }
                
                var xmlDoc = XDocument.Load(xmlFilePath);
                var envelope = xmlDoc.Element("ENVELOPE");
                if (envelope == null)
                {
                    Logger.Error("Invalid XML structure: ENVELOPE element not found");
                    return null;
                }

                var fileNameLists = envelope.Element("FILENAMELIST")?.Elements("FILENAMELIST").ToList();
                if (fileNameLists == null || fileNameLists.Count < 10)
                {
                    Logger.Error($"Invalid or incomplete XML structure. Expected at least 10 FILENAMELIST elements, found: {fileNameLists?.Count ?? 0}");
                    return null;
                }

                var xmlData = new XmlData();

                // 0: Input file paths
                foreach (var fileElem in fileNameLists[0].Elements("FILENAME"))
                {
                    string path = fileElem.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(path))
                        xmlData.InputFilePaths.Add(path);
                }
                Logger.Debug($"Input files found in XML: {xmlData.InputFilePaths.Count}");

                // 1: Output folder path
                xmlData.OutputFolderPath = fileNameLists[1].Element("FILENAME")?.Value.Trim();

                // 2: Common name
                xmlData.CommonName = fileNameLists[2].Element("FILENAME")?.Value.Trim();

                // 3: PIN
                xmlData.Pin = fileNameLists[3].Element("FILENAME")?.Value.Trim();

                // 4–7: Coordinates and dimensions
                float.TryParse(fileNameLists[4].Element("FILENAME")?.Value.Trim(), out float x);
                float.TryParse(fileNameLists[5].Element("FILENAME")?.Value.Trim(), out float y);
                float.TryParse(fileNameLists[6].Element("FILENAME")?.Value.Trim(), out float w);
                float.TryParse(fileNameLists[7].Element("FILENAME")?.Value.Trim(), out float h);

                xmlData.XCoordinate = x;
                xmlData.YCoordinate = y;
                xmlData.Width = w;
                xmlData.Height = h;

                // 8: Reserved/unused (was Sign On Page) - kept only for positional compatibility

                // 9: Open output folder
                xmlData.OpenOutputFolder = fileNameLists[9].Element("FILENAME")?.Value.Trim() ?? "Y";

                // Optional index 10: USESELFSIGNED flag
                if (fileNameLists.Count > 10)
                {
                    string flag = fileNameLists[10].Element("FILENAME")?.Value.Trim().ToUpper();
                    xmlData.UseSelfSigned = (flag == "Y");
                }

                // Optional indices 16-25: copy-label settings
                if (fileNameLists.Count > 16)
                {
                    string copy1Label = fileNameLists[16].Element("FILENAME")?.Value.Trim();
                    xmlData.Copy1Label = string.IsNullOrWhiteSpace(copy1Label) ? "Original for Buyer" : copy1Label;

                    string extraCopiesFlag = fileNameLists[17].Element("FILENAME")?.Value.Trim().ToUpper();
                    xmlData.ExtraCopiesEnabled = (extraCopiesFlag == "Y");

                    string printAllFlag = fileNameLists[18].Element("FILENAME")?.Value.Trim().ToUpper();
                    xmlData.PrintAllCopies = (printAllFlag == "Y");

                    xmlData.Copy2Label = fileNameLists[19].Element("FILENAME")?.Value.Trim();
                    xmlData.Copy3Label = fileNameLists[20].Element("FILENAME")?.Value.Trim();
                    xmlData.Copy4Label = fileNameLists[21].Element("FILENAME")?.Value.Trim();

                    if (float.TryParse(fileNameLists[22].Element("FILENAME")?.Value.Trim(), out float copyX))
                        xmlData.CopyLabelX = copyX;
                    if (float.TryParse(fileNameLists[23].Element("FILENAME")?.Value.Trim(), out float copyY))
                        xmlData.CopyLabelY = copyY;
                    if (float.TryParse(fileNameLists[24].Element("FILENAME")?.Value.Trim(), out float copyW))
                        xmlData.CopyLabelWidth = copyW;
                    if (float.TryParse(fileNameLists[25].Element("FILENAME")?.Value.Trim(), out float copyH))
                        xmlData.CopyLabelHeight = copyH;
                }

                Logger.Info("XML configuration loaded successfully");
                return xmlData;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error parsing XML configuration", ex);
                return null;
            }
        }

        static X509Certificate2 GetCertificate(string commonName)
        {
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(X509FindType.FindBySubjectName, commonName, false);
                if (certs.Count > 0)
                    return certs[0];
            }
            return null;
        }
    }
}
