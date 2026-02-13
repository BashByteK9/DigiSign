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
    /// Provides file-based logging with thread-safe operations
    /// </summary>
    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "application_log.txt");
        private static readonly string PlfLogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plf.txt");
        private static bool logInitialized = false;
        private static readonly object logLock = new object();

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
                    File.AppendAllText(LogFilePath, new string('-', 80) + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail
            }
        }
    }
}
