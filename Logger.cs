using System;
using System.IO;
using System.Text;

namespace DigiSign
{
    /// <summary>
    /// Log level enumeration for categorizing log messages
    /// </summary>
    public enum LogLevel
    {
        DEBUG,
        INFO,
        WARNING,
        ERROR,
        CRITICAL
    }

    /// <summary>
    /// Static logger class for application-wide logging functionality
    /// Provides file-based logging with thread-safe operations and automatic log rotation
    /// </summary>
    public static class Logger
    {
        private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        private static readonly string PlfLogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plf.txt");
        private static readonly long MaxLogFileSizeBytes = 1 * 1024 * 1024; // 1 MB
        private static string currentLogFilePath;
        private static bool logInitialized = false;
        private static readonly object logLock = new object();

        /// <summary>
        /// Ensures the logs directory exists
        /// </summary>
        private static void EnsureLogDirectoryExists()
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }

        /// <summary>
        /// Gets the current log file path, creating a new one if needed
        /// </summary>
        private static string GetLogFilePath()
        {
            if (string.IsNullOrEmpty(currentLogFilePath) || !File.Exists(currentLogFilePath))
            {
                currentLogFilePath = Path.Combine(LogDirectory, "application_log.txt");
            }
            return currentLogFilePath;
        }

        /// <summary>
        /// Checks if log rotation is needed and rotates the log file if necessary
        /// </summary>
        /// <param name="logFilePath">Path to the log file to check</param>
        private static void RotateLogIfNeeded(string logFilePath)
        {
            if (File.Exists(logFilePath))
            {
                var fileInfo = new FileInfo(logFilePath);
                if (fileInfo.Length >= MaxLogFileSizeBytes)
                {
                    // Create a timestamped backup of the current log
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string fileName = Path.GetFileNameWithoutExtension(logFilePath);
                    string extension = Path.GetExtension(logFilePath);
                    string rotatedLogPath = Path.Combine(LogDirectory, $"{fileName}_{timestamp}{extension}");

                    File.Move(logFilePath, rotatedLogPath);

                    // Update the current file path reference
                    currentLogFilePath = logFilePath;
                }
            }
        }

        /// <summary>
        /// Initializes the logger and creates log file with header information
        /// </summary>
        public static void Initialize()
        {
            lock (logLock)
            {
                if (!logInitialized)
                {
                    try
                    {
                        EnsureLogDirectoryExists();

                        string logFilePath = GetLogFilePath();

                        // Create log header
                        var header = new StringBuilder();
                        header.AppendLine("═══════════════════════════════════════════════════════════");
                        header.AppendLine($"DigiSign Application Log - Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        header.AppendLine($"Application Path: {AppDomain.CurrentDomain.BaseDirectory}");
                        header.AppendLine($"Machine: {Environment.MachineName} | User: {Environment.UserName}");
                        header.AppendLine($"OS: {Environment.OSVersion} | .NET: {Environment.Version}");
                        header.AppendLine("═══════════════════════════════════════════════════════════");
                        header.AppendLine();

                        // Append to existing log file instead of overwriting
                        File.AppendAllText(logFilePath, header.ToString());
                        logInitialized = true;

                        Log(LogLevel.INFO, "Logger initialized successfully");
                    }
                    catch (Exception ex)
                    {
                        // Failed to initialize logger - silently fail
                    }
                }
            }
        }

        /// <summary>
        /// Logs a message with the specified log level and optional exception
        /// </summary>
        /// <param name="level">Log level (DEBUG, INFO, WARNING, ERROR, CRITICAL)</param>
        /// <param name="message">Log message</param>
        /// <param name="ex">Optional exception to log</param>
        public static void Log(LogLevel level, string message, Exception ex = null)
        {
            try
            {
                if (!logInitialized)
                    Initialize();

                lock (logLock)
                {
                    string logFilePath = GetLogFilePath();
                    RotateLogIfNeeded(logFilePath);

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

                    File.AppendAllText(logFilePath, logEntry.ToString() + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail to avoid breaking the application
            }
        }

        /// <summary>
        /// Logs a debug message
        /// </summary>
        /// <param name="message">Debug message</param>
        public static void Debug(string message) => Log(LogLevel.DEBUG, message);

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message">Info message</param>
        public static void Info(string message) => Log(LogLevel.INFO, message);

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">Warning message</param>
        public static void Warning(string message) => Log(LogLevel.WARNING, message);

        /// <summary>
        /// Logs an error message with optional exception
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="ex">Optional exception</param>
        public static void Error(string message, Exception ex = null) => Log(LogLevel.ERROR, message, ex);

        /// <summary>
        /// Logs a critical error message with optional exception
        /// </summary>
        /// <param name="message">Critical error message</param>
        /// <param name="ex">Optional exception</param>
        public static void Critical(string message, Exception ex = null) => Log(LogLevel.CRITICAL, message, ex);

        /// <summary>
        /// Logs a message to the PLF (Print Log File) for external processing
        /// </summary>
        /// <param name="message">Message to log to PLF</param>
        /// <param name="isError">Whether this is an error message</param>
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

        /// <summary>
        /// Adds a separator line to the log file for better readability
        /// </summary>
        public static void LogSeparator()
        {
            try
            {
                if (!logInitialized)
                    Initialize();

                lock (logLock)
                {
                    string logFilePath = GetLogFilePath();
                    RotateLogIfNeeded(logFilePath);
                    File.AppendAllText(logFilePath, new string('-', 80) + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail
            }
        }
    }
}
