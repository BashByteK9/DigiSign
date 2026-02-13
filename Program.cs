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

namespace DigiSign
{
    /// <summary>
    /// Version information helper class with auto-incrementing build number
    /// </summary>
    public static class VersionInfo
    {
        private static readonly System.Version _assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        private static readonly DateTime _buildDate = GetBuildDate(Assembly.GetExecutingAssembly());
        
        /// <summary>
        /// Gets the build date from the assembly
        /// </summary>
        private static DateTime GetBuildDate(Assembly assembly)
        {
            const string BuildVersionMetadataPrefix = "+build";
            
            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute?.InformationalVersion != null)
            {
                var value = attribute.InformationalVersion;
                var index = value.IndexOf(BuildVersionMetadataPrefix);
                if (index > 0)
                {
                    value = value.Substring(index + BuildVersionMetadataPrefix.Length);
                    if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var result))
                    {
                        return result;
                    }
                }
            }
            
            // Fallback: Use assembly file's last write time
            return File.GetLastWriteTime(assembly.Location);
        }
        
        /// <summary>
        /// Gets the calculated build number based on date (days since 2000-01-01)
        /// </summary>
        private static int BuildNumber
        {
            get
            {
                var baseDate = new DateTime(2000, 1, 1);
                var days = (int)(_buildDate - baseDate).TotalDays;
                return days;
            }
        }
        
        /// <summary>
        /// Gets the calculated revision number (seconds since midnight / 2)
        /// </summary>
        private static int RevisionNumber
        {
            get
            {
                var midnight = _buildDate.Date;
                var seconds = (int)(_buildDate - midnight).TotalSeconds;
                return seconds / 2;
            }
        }
        
        /// <summary>
        /// Gets the full version string (e.g., "1.0.9145.31234")
        /// </summary>
        public static string FullVersion => $"{_assemblyVersion.Major}.{_assemblyVersion.Minor}.{BuildNumber}.{RevisionNumber}";
        
        /// <summary>
        /// Gets the short version string (e.g., "1.0.9145")
        /// </summary>
        public static string ShortVersion => $"{_assemblyVersion.Major}.{_assemblyVersion.Minor}.{BuildNumber}";
        
        /// <summary>
        /// Gets the version for display in title bars (e.g., "v1.0.9145")
        /// </summary>
        public static string DisplayVersion => $"v{ShortVersion}";
        
        /// <summary>
        /// Gets the application title with version (e.g., "DigiSign v1.0.9145")
        /// </summary>
        public static string TitleWithVersion => $"DigiSign {DisplayVersion}";
        
        /// <summary>
        /// Gets the build date and time
        /// </summary>
        public static string BuildDate => _buildDate.ToString("yyyy-MM-dd HH:mm:ss");
    }

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
        public bool VerboseMode { get; set; } = false;

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
            // Set console encoding to UTF-8 to properly display special characters
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
            }
            catch
            {
                // Silently fail if encoding cannot be set (e.g., when not running in console)
            }
            
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
            
            // Read XML data first to check verbose mode setting
            var xmlData = ReadXmlData(xmlFilePath);
            
            // Only enable verbose mode if NOT in admin mode or settings mode
            // Admin mode and settings mode are for configuration only, not PDF signing
            if (!isAdminMode && !isSettingsMode)
            {
                // Check for verbose mode from command line OR from XML settings
                bool cmdLineVerbose = args.Any(a => a.Equals("/verbose", StringComparison.OrdinalIgnoreCase));
                bool xmlVerbose = xmlData?.VerboseMode ?? false;
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
                // Admin mode or settings mode - verbose mode is not applicable
                if (isAdminMode)
                    Logger.Info("Admin mode - verbose mode disabled");
                else
                    Logger.Info("Settings mode - verbose mode disabled");
                    
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
                
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine($"⚙ {VersionInfo.TitleWithVersion} - Settings Configuration Mode");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine();
                Console.WriteLine("Opening settings panel...");
                Console.WriteLine();
                
                // Show settings panel without license requirement
                RunSettingsMode();
                return; // Exit after settings mode completes
            }

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
                Console.WriteLine($"🔑 {VersionInfo.TitleWithVersion} - Admin License Generation Mode");
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

                    // Use DigitalSignatureService for all signing operations
                    var signatureService = new DigitalSignatureService();
                    var cert = signatureService.LoadCertificate(commonName, pin, xmlData);

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

                        // Create signature configuration
                        var signatureConfig = new SignatureConfiguration(xCoord, yCoord, width, height, signOnPage);

                        // Process each PDF file
                        int successCount = 0;
                        int failCount = 0;
                        List<string> successfulFiles = new List<string>(); // Track successful files
                        
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
                                signatureService.SignPdf(inputPdfPath, outputPdfPath, cert, signatureConfig, pin, outputFolderPath, isVerboseMode, verboseForm);
                                successCount++;
                                successfulFiles.Add(outputFileName); // Track successful file
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
                        
                        // Store success info for PLF logging later (after verbose dialog closes)
                        hasSuccessfulSigning = successCount > 0;
                        if (successCount >= 1)
                        {
                            plfSuccessMessage = $"File(s) Signed Successfully - {successfulFiles[0]}";
                        }
                        
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
        
        static void RunSettingsMode()
        {
            // Settings mode - no license required
            Logger.Info("Entering settings mode - no license required");
            
            Console.WriteLine("Opening settings panel...");
            Console.WriteLine("Configure your PDF signing settings without requiring admin privileges.");
            Console.WriteLine();
            
            try
            {
                Logger.Debug("Creating LicenseGenerationForm for settings only");
                
                using (var form = new LicenseGenerationForm(settingsOnly: true))
                {
                    Logger.Debug("Showing settings form");
                    form.ShowDialog();
                    Logger.Debug("Settings form closed");
                }
                
                Console.WriteLine();
                Console.WriteLine("Settings panel closed.");
                Logger.Info("Settings mode completed");
            }
            catch (Exception ex)
            {
                Logger.Error("Error showing settings panel", ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine($"ERROR: Failed to show settings panel: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
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
                
                // Optional index 11: VerboseMode flag
                if (fileNameLists.Count > 11)
                {
                    string verboseFlag = fileNameLists[11].Element("FILENAME")?.Value.Trim().ToUpper();
                    xmlData.VerboseMode = (verboseFlag == "Y");
                    Logger.Debug($"VerboseMode from XML: {xmlData.VerboseMode}");
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
