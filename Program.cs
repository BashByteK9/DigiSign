using System;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Security;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Diagnostics;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Spire.Pdf.Graphics;
using System.Security.AccessControl;
using System.Text;
using System.Management;

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
        public string SignOnPage { get; set; } // F=First, E=Each, L=Last
        public string OpenOutputFolder { get; set; } // Y=Open, N=Not open
        public bool UseSelfSigned { get; set; } = false;

    }

    public class PdfSignatureValidator
    {
        public class SignatureValidationResult
        {
            public string SignatureName { get; set; }
            public bool IsValid { get; set; }
        }

        public static List<SignatureValidationResult> ValidateSignatures(string pdfPath)
        {
            var results = new List<SignatureValidationResult>();

            using (PdfReader reader = new PdfReader(pdfPath))
            {
                AcroFields af = reader.AcroFields;
                var signatureNames = af.GetSignatureNames();

                foreach (var name in signatureNames)
                {
                    PdfPKCS7 pkcs7 = af.VerifySignature(name);
                    bool valid = pkcs7.Verify();

                    results.Add(new SignatureValidationResult
                    {
                        SignatureName = name,
                        IsValid = valid
                    });
                }
            }

            return results;
        }
    }



    public enum LogLevel
    {
        DEBUG,
        INFO,
        WARNING,
        ERROR,
        CRITICAL
    }

    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "application_log.txt");
        private static readonly string PlfLogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plf.txt");
        private static bool logInitialized = false;
        private static readonly object logLock = new object();

        public static void Initialize()
        {
            lock (logLock)
            {
                if (!logInitialized)
                {
                    try
                    {
                        // Create log header
                        var header = new StringBuilder();
                        header.AppendLine("═══════════════════════════════════════════════════════════");
                        header.AppendLine($"DigiSign Application Log - Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        header.AppendLine($"Application Path: {AppDomain.CurrentDomain.BaseDirectory}");
                        header.AppendLine($"Machine: {Environment.MachineName} | User: {Environment.UserName}");
                        header.AppendLine($"OS: {Environment.OSVersion} | .NET: {Environment.Version}");
                        header.AppendLine("═══════════════════════════════════════════════════════════");
                        header.AppendLine();

                        File.WriteAllText(LogFilePath, header.ToString());
                        logInitialized = true;
                        
                        Log(LogLevel.INFO, "Logger initialized successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to initialize logger: {ex.Message}");
                    }
                }
            }
        }

        public static void Log(LogLevel level, string message, Exception ex = null)
        {
            try
            {
                if (!logInitialized)
                    Initialize();

                lock (logLock)
                {
                    var logEntry = new StringBuilder();
                    logEntry.Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    logEntry.Append($" | {level,-8}");
                    logEntry.Append($" | {message}");

                    if (ex != null)
                    {
                        logEntry.AppendLine();
                        logEntry.Append($"{"",23} | Exception: {ex.GetType().Name} - {ex.Message}");
                        if (!string.IsNullOrEmpty(ex.StackTrace))
                        {
                            logEntry.AppendLine();
                            logEntry.Append($"{"",23} | StackTrace: {ex.StackTrace.Replace(Environment.NewLine, Environment.NewLine + new string(' ', 23) + " | ")}");
                        }
                    }

                    File.AppendAllText(LogFilePath, logEntry.ToString() + Environment.NewLine);

                    // Also write to console for ERROR and CRITICAL
                    if (level == LogLevel.ERROR || level == LogLevel.CRITICAL)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[{level}] {message}");
                        if (ex != null)
                            Console.WriteLine($"  → {ex.Message}");
                        Console.ResetColor();
                    }
                    else if (level == LogLevel.WARNING)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[{level}] {message}");
                        Console.ResetColor();
                    }
                }
            }
            catch
            {
                // Silently fail to avoid breaking the application
            }
        }

        public static void Debug(string message) => Log(LogLevel.DEBUG, message);
        public static void Info(string message) => Log(LogLevel.INFO, message);
        public static void Warning(string message) => Log(LogLevel.WARNING, message);
        public static void Error(string message, Exception ex = null) => Log(LogLevel.ERROR, message, ex);
        public static void Critical(string message, Exception ex = null) => Log(LogLevel.CRITICAL, message, ex);

        public static void LogToPlf(string message, bool isError = false)
        {
            try
            {
                lock (logLock)
                {
                    // Write only the message to PLF file (no timestamp, no status prefix)
                    File.WriteAllText(PlfLogFilePath, message + Environment.NewLine);
                    
                    // Still log to application log with full details
                    if (isError)
                        Error($"PLF Log: {message}");
                    else
                        Info($"PLF Log: {message}");
                }
            }
            catch (Exception ex)
            {
                Error("Failed to write to PLF log file", ex);
            }
        }

        public static void LogSeparator()
        {
            try
            {
                if (!logInitialized)
                    Initialize();

                lock (logLock)
                {
                    File.AppendAllText(LogFilePath, new string('-', 80) + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail
            }
        }
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
            Logger.Info("Application started");
            Logger.Debug($"Command line arguments: {string.Join(" ", args)}");

            // Check for verbose mode
            isVerboseMode = args.Any(a => a.Equals("/verbose", StringComparison.OrdinalIgnoreCase));
            shouldAutoClose = isVerboseMode;

            if (isVerboseMode)
            {
                Logger.Info("Verbose mode enabled");
                
                // Create and show verbose progress form
                verboseForm = new VerboseProgressForm();
                verboseForm.Show();
                verboseForm.AppendText("═══════════════════════════════════════════════════════════\n", Color.Gray, true);
                verboseForm.AppendText("DigiSign - VERBOSE MODE\n", Color.FromArgb(0, 102, 204), true);
                verboseForm.AppendText("═══════════════════════════════════════════════════════════\n\n", Color.Gray, true);
                Application.DoEvents(); // Process form events
            }

            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            string licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.txt");
            string xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP.xml");
            
            if (isVerboseMode)
            {
                verboseForm.UpdateProgress(1, "Initializing application...");
                verboseForm.AppendDetail($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
                Application.DoEvents();
            }
            
            Logger.Debug($"License file path: {licensePath}");
            Logger.Debug($"XML configuration file path: {xmlFilePath}");
            
            if (isVerboseMode)
            {
                verboseForm.UpdateProgress(2, "Loading configuration...");
                verboseForm.AppendDetail($"License file: {Path.GetFileName(licensePath)}");
                verboseForm.AppendDetail($"Config file: {Path.GetFileName(xmlFilePath)}");
                Application.DoEvents();
            }
            
            
            var xmlData = ReadXmlData(xmlFilePath);
            int totalErrorCount = 0; // Track ALL errors (validation, signing, etc.) for auto-close timing

            // Declare adminLicensePath once for the entire method scope
            string adminLicensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "admin.license");

            // Check if admin mode is requested
            bool isAdminMode = args.Length > 0 && args[0].Equals("/admin", StringComparison.OrdinalIgnoreCase);

            if (isAdminMode)
            {
                // ADMIN MODE: Only requires admin license, not user license
                Logger.Info("Admin mode requested - checking for admin license");
                
                if (!File.Exists(adminLicensePath))
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine("ERROR: Admin license not found!");
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine($"Please ensure 'admin.license' file exists in: {AppDomain.CurrentDomain.BaseDirectory}");
                    Console.WriteLine("Contact support for an admin license if you don't have one.");
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                if (!ValidateAdminLicense(adminLicensePath))
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine("ERROR: Invalid admin license!");
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("The admin.license file is invalid or corrupted.");
                    Console.WriteLine("Contact support for a valid admin license.");
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                // Admin license is valid, proceed with admin mode
                Logger.Info("Valid admin license detected - proceeding with license generation");
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("🔑 Admin License Generation Mode");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                
                // Continue with admin license generation logic...
                // (existing admin mode code will be here)
                RunAdminMode(adminLicensePath);
                return; // Exit after admin mode completes
            }

            // PDF SIGNING MODE: Requires user license (license.txt)
            Logger.LogSeparator();
            Logger.Info("Starting license validation for PDF signing");

            if (isVerboseMode)
            {
                verboseForm.UpdateProgress(3, "Validating license...");
                Application.DoEvents();
            }

            // Check if admin license exists (for hint message later)
            bool hasAdminLicense = File.Exists(adminLicensePath) && ValidateAdminLicense(adminLicensePath);
            
            // Check user license for PDF signing
            bool hasValidUserLicense = false;
            int licenseExpiryDaysRemaining = -1; // Track days until expiry
            
            if (File.Exists(licensePath))
            {
                Logger.Info($"License file found at: {licensePath}");
                if (ValidateLicense(licensePath))
                {
                    hasValidUserLicense = true;
                    
                    // Get days remaining for expiry check
                    licenseExpiryDaysRemaining = GetLicenseExpiryDays(licensePath);
                    
                    Console.WriteLine("✅ License valid — Full Mode enabled.");
                    Logger.Info("License validation successful - Full Mode enabled");
                    if (isVerboseMode)
                    {
                        verboseForm.AppendSuccess("LICENSED - Full cryptographic signing enabled");
                        Application.DoEvents();
                    }
                }
                else
                {
                    Console.WriteLine("❌ License invalid or used on a different device.");
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
                Console.WriteLine("❌ License file not found.");
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
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("ERROR: Valid user license required for PDF signing!");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Please ensure you have a valid license.txt file.");
                Console.WriteLine("Contact support for a license if you don't have one.");
                Console.WriteLine();
                
                // Generate license.key file if it doesn't exist
                string licenseKeyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.key");
                if (!File.Exists(licenseKeyPath))
                {
                    Logger.Info("Generating license.key file for user to send to admin");
                    GenerateLicenseKeyFile(licenseKeyPath);
                }
                else
                {
                    Logger.Info("license.key file already exists");
                    Console.WriteLine($"📄 License key file exists at: {licenseKeyPath}");
                    Console.WriteLine("   Send this file to your administrator to generate license.txt");
                    Console.WriteLine();
                }
                
                if (hasAdminLicense)
                {
                    Console.WriteLine("💡 Admin license detected. Use /admin flag to generate user licenses.");
                    Console.WriteLine("   Example: DigiSign.exe /admin");
                    Console.WriteLine();
                    Console.WriteLine("Note: Admin licenses cannot be used for PDF signing.");
                    Console.WriteLine();
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
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                }
                return;
            }




            Logger.Info("Application mode: FULL (valid user license)");
            
            // Show license expiration warning if expiring within 15 days
            if (licenseExpiryDaysRemaining >= 0 && licenseExpiryDaysRemaining <= 15)
            {
                Logger.Info($"License expires in {licenseExpiryDaysRemaining} days - showing warning dialog");
                ShowLicenseExpirationWarning(licenseExpiryDaysRemaining);
            }
            else if (licenseExpiryDaysRemaining > 15)
            {
                Logger.Debug($"License has {licenseExpiryDaysRemaining} days remaining - no warning needed");
            }
            
            Logger.LogSeparator();

            // Show hint if user has admin license but didn't use /admin flag
            if (File.Exists(adminLicensePath) && ValidateAdminLicense(adminLicensePath))
            {
                Console.WriteLine("💡 Admin license detected. Run with /admin flag to generate licenses.");
                Console.WriteLine("   Example: DigiSign.exe /admin");
                Console.WriteLine();
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
                string signOnPage = xmlData.SignOnPage ?? "L"; // Default to Last page
                string openOutputFolder = xmlData.OpenOutputFolder ?? "Y"; // Default to Yes

                Logger.Debug($"Signature coordinates: X={xCoord}, Y={yCoord}, Width={width}, Height={height}");
                Logger.Debug($"Sign on page: {signOnPage}");

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
                    
                    Logger.Info($"Loading certificate: {commonName}");
                    var cert = LoadCertificateFromUSBToken(commonName, pin, xmlData);

                    if (cert != null)
                    {
                        Logger.Info("Certificate loaded successfully");
                        Logger.Debug($"Certificate Subject: {cert.Subject}");
                        Logger.Debug($"Certificate Thumbprint: {cert.Thumbprint}");
                        Logger.Debug($"Certificate Expiry: {cert.NotAfter:yyyy-MM-dd}");
                        
                        if (isVerboseMode)
                        {
                            VerboseLog("Certificate loaded", VerboseLogType.Success);
                            VerboseLog($"Subject: {cert.Subject}", VerboseLogType.Detail);
                            VerboseLog($"Expiry: {cert.NotAfter:yyyy-MM-dd}", VerboseLogType.Detail);
                            verboseForm.AppendText("\n", Color.Black);
                            verboseForm.UpdateProgress(8, "Processing PDF files...");
                            verboseForm.AppendText("\n", Color.Black);
                            Application.DoEvents();
                        }
                        
                        // Process each PDF file
                        int successCount = 0;
                        int failCount = 0;
                        
                        for (int i = 0; i < validPdfFiles.Count; i++)
                        {
                            string inputPdfPath = validPdfFiles[i];
                            
                            Logger.LogSeparator();
                            Logger.Info($"Processing PDF: {Path.GetFileName(inputPdfPath)}");
                            
                            if (isVerboseMode)
                            {
                                verboseForm.AppendText($"\n    PDF {i + 1}/{validPdfFiles.Count}: {Path.GetFileName(inputPdfPath)}\n", Color.FromArgb(0, 102, 204), true);
                                Application.DoEvents();
                            }
                            
                            string inputFileName = Path.GetFileName(inputPdfPath);
                            string outputFileName = $"{inputFileName}";
                            string outputPdfPath = Path.Combine(outputFolderPath, outputFileName);

                            try
                            {
                                SignPdfWithITextSharp(inputPdfPath, outputPdfPath, cert, xCoord, yCoord, width, height, signOnPage, pin, outputFolderPath);
                                successCount++;
                                Logger.Info($"Successfully signed: {inputFileName}");
                                
                                if (isVerboseMode)
                                {
                                    VerboseLog("SUCCESS", VerboseLogType.Success);
                                    VerboseLog($"Output: {outputFileName}", VerboseLogType.Detail);
                                    verboseForm.AppendText("\n", Color.Black);
                                    Application.DoEvents();
                                }
                            }
                            catch (Exception ex)
                            {
                                failCount++;
                                Logger.Error($"Failed to sign: {inputFileName}", ex);
                                
                                if (isVerboseMode)
                                {
                                    VerboseLog("FAILED", VerboseLogType.Error);
                                    VerboseLog($"Error: {ex.Message}", VerboseLogType.Detail);
                                    verboseForm.AppendText("\n", Color.Black);
                                    Application.DoEvents();
                                }
                            }
                        }

                        Logger.LogSeparator();
                        Logger.Info($"PDF signing completed - Success: {successCount}, Failed: {failCount}");
                        
                        // Add PDF signing failures to total error count
                        totalErrorCount += failCount;
                        
                        if (isVerboseMode)
                        {
                            verboseForm.UpdateProgress(9, "Processing complete");
                            verboseForm.AppendText("\n", Color.Black);
                            verboseForm.ShowSummary(successCount, failCount);
                            verboseForm.AppendText("\n", Color.Black);
                            Application.DoEvents();
                        }

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
                    else
                    {
                        Logger.Error($"Certificate not found: {commonName}");
                        Logger.LogToPlf($"Certificate not found: {commonName}", isError: true);
                        totalErrorCount++; // Count certificate not found error
                        
                        if (isVerboseMode)
                        {
                            VerboseLog($"Certificate not found: {commonName}", VerboseLogType.Error);
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
                return;
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

        static bool ValidateLicense(string filePath)
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

        static string GenerateDeviceHash(string deviceId, string licenseNumber, string validUntil)
        {
            // Include ValidUntil in hash to prevent date tampering
            string data = deviceId + "|" + licenseNumber + "|" + validUntil;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        static string GetDeviceId()
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

        static int GetLicenseExpiryDays(string filePath)
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

        static void ShowLicenseExpirationWarning(int daysRemaining)
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

        static void GenerateLicenseKeyFile(string licenseKeyPath)
        {
            try
            {
                if (File.Exists(licenseKeyPath))
                {
                    Logger.Info($"License key file already exists at: {licenseKeyPath}");
                    Console.WriteLine($"📄 License key file already exists at: {licenseKeyPath}");
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

                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("📄 License Key File Generated");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine($"Location: {licenseKeyPath}");
                Console.WriteLine($"Device ID: {deviceId}");
                Console.WriteLine();
                Console.WriteLine("Please share the license.key file with your administrator");
                Console.WriteLine("to generate a valid license.txt file.");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Logger.Error("Error generating license key file", ex);
                Console.WriteLine($"Error generating license key file: {ex.Message}");
            }
        }

        static bool ValidateAdminLicense(string adminLicensePath)
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
                    // Don't log details - security risk
                    return false;
                }

                // Validate expiration
                if (DateTime.TryParse(adminData["ValidUntil"], out var validDate) && validDate < DateTime.Now)
                {
                    // Don't log details - security risk
                    Console.WriteLine("⚠️ Admin license has expired.");
                    return false;
                }

                // Simple validation - in production, you'd validate AdminKey against AdminID
                string expectedKey = GenerateAdminKey(adminData["AdminID"]);
                if (adminData["AdminKey"] != expectedKey)
                {
                    // Don't log details - security risk
                    Console.WriteLine("⚠️ Invalid admin license key.");
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

        static string GenerateAdminKey(string adminId)
        {
            // Simple hash for admin key - in production use a more secure method
            using (SHA256 sha = SHA256.Create())
            {
                string data = adminId + "|DIGISIGN_ADMIN_SECRET";
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        // Helper class to pass form results
        class LicenseGenerationResult
        {
            public string LicenseKeyPath { get; set; }
            public string CustomerId { get; set; }
            public string LicenseNumber { get; set; }
            public DateTime ExpirationDate { get; set; }
            public bool WasCancelled { get; set; }
        }


        static void RunAdminMode(string adminLicensePath)
        {
            // Admin license is valid, proceed with license generation
            Logger.Info("Admin license validated - entering license generation mode");
            Console.WriteLine("✅ Admin license validated");
            Console.WriteLine();
            
            Console.WriteLine("This mode is ONLY for generating user licenses.");
            Console.WriteLine("No PDF signing will be performed.");
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();
            
            // Use Windows Forms GUI for admin mode
            Logger.Debug("Showing License Generation Form");
            
            Console.WriteLine("Opening License Generation Form...");
            Console.WriteLine("(Please fill in the form that appears)");
            Console.WriteLine();
            
            LicenseGenerationResult result = ShowLicenseGenerationForm();
            
            if (result == null || result.WasCancelled)
            {
                Console.WriteLine();
                Console.WriteLine("License generation cancelled by user.");
                Logger.Info("Admin mode exited - User cancelled the license generation form");
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            
            Logger.Debug($"User input received from form");
            Logger.Debug($"  License Key Path: {result.LicenseKeyPath}");
            Logger.Debug($"  Customer ID: {result.CustomerId}");
            Logger.Debug($"  License Number: {result.LicenseNumber}");
            Logger.Debug($"  Expiration Date: {result.ExpirationDate:yyyy-MM-dd}");
            
            // Generate license from the form data
            Console.WriteLine();
            Console.WriteLine($"✅ License key file: {Path.GetFileName(result.LicenseKeyPath)}");
            Console.WriteLine($"✅ Customer ID: {result.CustomerId}");
            Console.WriteLine($"✅ License Number: {result.LicenseNumber}");
            Console.WriteLine($"✅ Expiration Date: {result.ExpirationDate:yyyy-MM-dd}");
            Console.WriteLine();
            
            if (GenerateLicenseFromForm(result))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("✅ LICENSE GENERATED SUCCESSFULLY!");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("The license.txt file has been created in the same folder");
                Console.WriteLine("as the license.key file. Please send this file to the user.");
                Logger.Info($"License file generated successfully from: {result.LicenseKeyPath}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("❌ Failed to generate license file.");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Please check:");
                Console.WriteLine("  1. The license.key file is valid");
                Console.WriteLine("  2. You have write permissions to the output folder");
                Console.WriteLine("  3. The application_log.txt for detailed error information");
                Logger.Error("Failed to generate license file");
            }
            
            Console.WriteLine();
            // Exit after admin operations - DO NOT process PDFs
            Logger.Info("Exiting admin mode (no PDF processing)");
            Logger.Info("Application ended");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static LicenseGenerationResult ShowLicenseGenerationForm()
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine($"ERROR: Failed to show license generation form: {ex.Message}");
                Console.ResetColor();
                return new LicenseGenerationResult { WasCancelled = true };
            }
        }

        static bool GenerateLicenseFromForm(LicenseGenerationResult formData)
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

                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("License Details:");
                Console.WriteLine($"  Customer ID: {formData.CustomerId}");
                Console.WriteLine($"  License Number: {formData.LicenseNumber}");
                Console.WriteLine($"  Valid Until: {formData.ExpirationDate:yyyy-MM-dd}");
                Console.WriteLine($"  Device ID: {deviceId}");
                Console.WriteLine($"  Output File: {licensePath}");
                Console.WriteLine("═══════════════════════════════════════════════════════════");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Error generating license from form data", ex);
                Console.WriteLine($"Error generating license: {ex.Message}");
                MessageBox.Show($"Error generating license: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine($"ERROR: Failed to show file selection dialog: {ex.Message}");
                Console.ResetColor();
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

                // 8: Sign on page
                xmlData.SignOnPage = fileNameLists[8].Element("FILENAME")?.Value.Trim() ?? "L";

                // 9: Open output folder
                xmlData.OpenOutputFolder = fileNameLists[9].Element("FILENAME")?.Value.Trim() ?? "Y";

                // Optional index 10: USESELFSIGNED flag
                if (fileNameLists.Count > 10)
                {
                    string flag = fileNameLists[10].Element("FILENAME")?.Value.Trim().ToUpper();
                    xmlData.UseSelfSigned = (flag == "Y");
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

        static bool CertificateMatchesCN(X509Certificate2 cert, string commonName)
        {
            // Extract CN part from the subject
            var cnPart = cert.Subject
                .Split(',')
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))?
                .Substring(3);

            return string.Equals(cnPart, commonName, StringComparison.OrdinalIgnoreCase);
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

        static X509Certificate2 LoadCertificateFromUSBToken(string commonName, string pin, XmlData xmlData)
        {
            Logger.Debug($"Loading certificate with CN: {commonName}");
            X509Store[] stores = new X509Store[]
            {
        new X509Store(StoreName.My, StoreLocation.CurrentUser),
        new X509Store(StoreName.My, StoreLocation.LocalMachine)
            };

            foreach (var store in stores)
            {
                try
                {
                    Logger.Debug($"Searching certificates in {store.Location}\\{store.Name} store");
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                    // Step 1: Try USB token certificate first
                    var certs = store.Certificates.Find(X509FindType.FindBySubjectName, commonName, false);
                    Logger.Debug($"Found {certs.Count} certificate(s) matching name: {commonName}");
                    
                    foreach (var cert in certs.Cast<X509Certificate2>())
                    {
                        Logger.Debug($"Certificate found - Subject: {cert.Subject}, Issuer: {cert.Issuer}, HasPrivateKey: {cert.HasPrivateKey}");

                        if (CertificateMatchesCN(cert, commonName))
                        {
                            if (cert.HasPrivateKey)
                            {
                                try
                                {
                                    using (var rsa = cert.GetRSAPrivateKey())
                                    {
                                        if (rsa is RSACryptoServiceProvider rsaCsp && rsaCsp.CspKeyContainerInfo.HardwareDevice)
                                        {
                                            cert.SetPinForPrivateKey(pin);
                                            Logger.Info("PIN set for hardware token certificate");
                                        }
                                        else
                                        {
                                            Logger.Debug("Certificate has private key, but not hardware token. No PIN set");
                                        }
                                    }
                                }
                                catch (CryptographicException ex)
                                {
                                    Logger.Warning($"Unable to access private key: {ex.Message}");
                                }
                            }

                            Logger.Info($"Certificate matched CN='{commonName}': {cert.Subject}");
                            return cert;
                        }


                        // Step 2: Check self-signed certificate if allowed
                        else if (xmlData.UseSelfSigned)
                        {
                            var selfSignedCert = store.Certificates
                                .Cast<X509Certificate2>()
                                .FirstOrDefault(c =>
                                    c.Subject == c.Issuer &&
                                    CertificateMatchesCN(c, commonName));

                            if (selfSignedCert != null)
                            {
                                Logger.Info($"Selected self-signed certificate: {selfSignedCert.Subject}");
                                return selfSignedCert;
                            }
                            else
                            {
                                Logger.Debug("No matching self-signed certificate found in this store");
                            }
                        }
                        else
                        {
                            Logger.Debug("Self-signed certificate selection disabled");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to open {store.Location}\\{store.Name} store", ex);
                }
                finally
                {
                    store.Close();
                }
            }

            // Not found
            Logger.Error($"No certificate found for CN='{commonName}' in any store");
            return null;
        }
        static void SignPdfWithITextSharp(string inputPath, string outputPath, X509Certificate2 cert, float x, float y, float width, float height, string signOnPage, string certPassword, string outputFolderPath)
        {
            try
            {
                Logger.Info("Starting PDF processing - Full cryptographic signing mode");
                
                if (isVerboseMode)
                {
                    VerboseLog("Reading PDF file...", VerboseLogType.Info);
                }

                // Extract CN from the certificate subject
                string cn = cert.Subject
                    .Split(',')
                    .Select(p => p.Trim())
                    .FirstOrDefault(p => p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring(3) ?? "Unknown";

                string signatureText = $"{cn}\nDigitally signed by {cn}\nDate: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

                Logger.Debug($"Signature text: {signatureText.Replace("\n", " | ")}");

                // Setup PDF reader
                PdfReader reader = new PdfReader(inputPath);
                int pageCount = reader.NumberOfPages;
                
                Logger.Debug($"PDF has {pageCount} pages");
                
                if (isVerboseMode)
                {
                    VerboseLog($"Pages: {pageCount}", VerboseLogType.Info);
                    VerboseLog("Signature mode: FULL (cryptographic)", VerboseLogType.Info);
                }

                // Determine which pages to process
                var pagesToProcess = new List<int>();
                switch (signOnPage?.ToUpper())
                {
                    case "F":
                        pagesToProcess.Add(1); // First page
                        if (isVerboseMode) VerboseLog("Signing: First page only", VerboseLogType.Info);
                        break;
                    case "E":
                        pagesToProcess.AddRange(Enumerable.Range(1, pageCount)); // Each page
                        if (isVerboseMode) VerboseLog($"Signing: All {pageCount} pages", VerboseLogType.Info);
                        break;
                    case "L":
                    default:
                        pagesToProcess.Add(pageCount); // Last page
                        if (isVerboseMode) VerboseLog("Signing: Last page only", VerboseLogType.Info);
                        break;
                }

                // Apply actual digital signature
                Logger.Info("Full mode: Applying cryptographic digital signature");
                
                if (isVerboseMode)
                {
                    VerboseLog("Creating signature...", VerboseLogType.Info);
                }
                
                using (FileStream os = new FileStream(outputPath, FileMode.Create))
                {
                        PdfStamper stamper = PdfStamper.CreateSignature(reader, os, '\0');
                        PdfSignatureAppearance appearance = stamper.SignatureAppearance;
                        appearance.CertificationLevel = PdfSignatureAppearance.CERTIFIED_NO_CHANGES_ALLOWED;
                        appearance.Reason = "Digitally signed";
                        appearance.Acro6Layers = false;

                        // Sign each specified page
                        foreach (int page in pagesToProcess)
                        {
                            iTextSharp.text.Rectangle pageSize = reader.GetPageSize(page);
                            float pageWidth = pageSize.Width;
                            float pageHeight = pageSize.Height;

                            // Validate coordinates
                            float adjustedX = x;
                            float adjustedY = y;
                            float adjustedWidth = width;
                            float adjustedHeight = height;

                            if (x < 0 || y < 0 || x + width > pageWidth || y + height > pageHeight)
                            {
                                Logger.Warning($"Signature rectangle outside page {page} boundaries. Adjusting coordinates");
                                Logger.Debug($"Original: X={x}, Y={y}, W={width}, H={height}, PageSize: {pageWidth}x{pageHeight}");
                                adjustedX = Math.Max(50, x);
                                adjustedY = Math.Max(50, y);
                                adjustedWidth = Math.Min(width, pageWidth - adjustedX - 50);
                                adjustedHeight = Math.Min(height, pageHeight - adjustedY - 50);
                                Logger.Debug($"Adjusted: X={adjustedX}, Y={adjustedY}, W={adjustedWidth}, H={adjustedHeight}");
                            }

                            // Define visible signature area for the current page
                            appearance.SetVisibleSignature(new iTextSharp.text.Rectangle(adjustedX, adjustedY, adjustedX + adjustedWidth, adjustedY + adjustedHeight), page, $"sig_{page}");

                            // Disable default Layer2 text to avoid double rendering
                            appearance.Layer2Text = string.Empty;

                            // Draw signature appearance
                            PdfContentByte over = stamper.GetOverContent(page);
                            DrawSignatureText(over, cn, signatureText, adjustedX, adjustedY, adjustedWidth, adjustedHeight);
                        }

                        if (isVerboseMode)
                        {
                            VerboseLog("Applying cryptographic signature...", VerboseLogType.Info);
                        }

                        // Create a custom implementation of IExternalSignature
                        IExternalSignature externalSignature = new SafeCertificateSignature(cert, "SHA-256");

                        // Convert the certificate to a BouncyCastle certificate
                        Org.BouncyCastle.X509.X509Certificate bcCert = DotNetUtilities.FromX509Certificate(cert);

                        // Sign the document
                        var ocspClient = new OcspClientBouncyCastle();
                        ITSAClient tsaClient = null;

                        try
                        {
                            Logger.Debug("Attempting to get timestamp from DigiCert");
                            if (isVerboseMode)
                            {
                                VerboseLog("Requesting timestamp...", VerboseLogType.Info);
                            }
                            tsaClient = new TSAClientBouncyCastle("http://timestamp.digicert.com");
                            Logger.Info("Timestamp service connected successfully");
                            if (isVerboseMode)
                            {
                                VerboseLog("Timestamp acquired", VerboseLogType.Success);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"TSA not available, proceeding without timestamp: {ex.Message}");
                            if (isVerboseMode)
                            {
                                VerboseLog("Timestamp unavailable (continuing without)", VerboseLogType.Warning);
                            }
                        }

                        MakeSignature.SignDetached(
                            appearance,
                            externalSignature,
                            new[] { bcCert },
                            null,
                            ocspClient,
                            tsaClient,
                            0,
                            CryptoStandard.CMS
                        );

                        Logger.Info($"PDF digitally signed successfully: {Path.GetFileName(outputPath)}");
                        Logger.LogToPlf($"File(s) Signed Successfully - {Path.GetFileName(outputPath)}", isError: false);
                        
                        if (isVerboseMode)
                        {
                            VerboseLog($"Saving signed PDF: {Path.GetFileName(outputPath)}", VerboseLogType.Info);
                        }
                    }

                reader.Close();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to process PDF: {Path.GetFileName(inputPath)}", ex);
                Logger.LogToPlf($"ERROR: Failed to process '{Path.GetFileName(inputPath)}' - {ex.Message}", isError: true);
                throw; // Re-throw to be caught in Main method
            }
        }

        static void DrawSignatureText(PdfContentByte over, string cn, string signatureText, float adjustedX, float adjustedY, float adjustedWidth, float adjustedHeight)
        {
            over.SaveState();

            // Draw text with wrapping
            BaseFont baseFontCN = BaseFont.CreateFont(BaseFont.TIMES_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
            BaseFont baseFontText = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);

            float fontSizeCN = 10;
            float fontSizeText = 9;
            float padding = 3;
            float maxTextWidth = adjustedWidth - 2 * padding;
            float leadingCN = fontSizeCN + 3;
            float leadingText = fontSizeText + 1;
            float maxY = adjustedY + adjustedHeight - padding;
            float minY = adjustedY + padding;
            float currentY = maxY;

            over.BeginText();

            // Draw CN with wrapping
            over.SetFontAndSize(baseFontCN, fontSizeCN);
            over.SetColorFill(BaseColor.BLACK);
            string cnLine = cn.Trim();
            if (!string.IsNullOrEmpty(cnLine))
            {
                List<string> wrappedCNLines = new List<string>();
                string[] words = cnLine.Split(' ');
                string currentLine = "";
                foreach (string word in words)
                {
                    string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    float lineWidth = baseFontCN.GetWidthPoint(testLine, fontSizeCN);
                    if (lineWidth <= maxTextWidth)
                    {
                        currentLine = testLine;
                    }
                    else
                    {
                        wrappedCNLines.Add(currentLine);
                        currentLine = word;
                    }
                }
                if (!string.IsNullOrEmpty(currentLine))
                    wrappedCNLines.Add(currentLine);

                foreach (string wrappedLine in wrappedCNLines)
                {
                    if (currentY - leadingCN < minY) break;
                    over.ShowTextAligned(Element.ALIGN_LEFT, wrappedLine, adjustedX + padding, currentY, 0);
                    currentY -= leadingCN;
                }
            }

            // Draw signature text (skip first CN line since we already drew it)
            over.SetFontAndSize(baseFontText, fontSizeText);
            var signatureLines = signatureText.Split('\n').Skip(1).ToList();
            
            Logger.Debug($"Drawing {signatureLines.Count} signature text lines");

            foreach (string rawLine in signatureLines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                Logger.Debug($"Processing signature line: {line}");

                List<string> wrappedLines = new List<string>();
                string[] words = line.Split(' ');
                string currentLine = "";

                foreach (string word in words)
                {
                    string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    float lineWidth = baseFontText.GetWidthPoint(testLine, fontSizeText);

                    if (lineWidth <= maxTextWidth)
                    {
                        currentLine = testLine;
                    }
                    else
                    {
                        wrappedLines.Add(currentLine);
                        currentLine = word;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    wrappedLines.Add(currentLine);
                }

                foreach (string wrappedLine in wrappedLines)
                {
                    if (currentY - leadingText < minY) break;

                    over.SetColorFill(BaseColor.BLACK);
                    over.ShowTextAligned(Element.ALIGN_LEFT, wrappedLine, adjustedX + padding, currentY, 0);
                    currentY -= leadingText;
                }
            }

            over.EndText();
            over.RestoreState();
        }



        public class SafeCertificateSignature : IExternalSignature
        {
            private readonly X509Certificate2 _certificate;
            private readonly string _hashAlgorithm;

            public SafeCertificateSignature(X509Certificate2 certificate, string hashAlgorithm)
            {
                _certificate = certificate;
                _hashAlgorithm = hashAlgorithm;
            }

            public string GetHashAlgorithm() => _hashAlgorithm;

            public string GetEncryptionAlgorithm() => "RSA";

            public byte[] Sign(byte[] message)
            {

                using (var rsa = _certificate.GetRSAPrivateKey())
                {
                    if (rsa == null)
                        throw new InvalidOperationException("RSA private key not found.");

                    HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA256;

                    // Use Windows to handle the PIN prompt and signing
                    return rsa.SignData(message, hashAlgorithm, RSASignaturePadding.Pkcs1);
                }
            }
        }
    }
}
