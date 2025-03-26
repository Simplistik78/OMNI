using System;
using System.IO;
using System.Text;
using System.Threading;

namespace OMNI.Services.Update
{
    /// <summary>
    /// Provides logging functionality specifically for the update process
    /// </summary>
    public class UpdateLoggerService
    {
        private static readonly object _logLock = new object();
        private static string _logFilePath;
        private static StringBuilder _currentSessionLog = new StringBuilder();

        static UpdateLoggerService()
        {
            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string logDirectory = Path.Combine(appDirectory, "Logs");

                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                _logFilePath = Path.Combine(logDirectory, $"update_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                // Write initial log header
                File.WriteAllText(_logFilePath, $"OMNI Update Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n");
                File.AppendAllText(_logFilePath, $"Application Directory: {appDirectory}\r\n");
                File.AppendAllText(_logFilePath, new string('-', 80) + "\r\n\r\n");
            }
            catch
            {
                // If we can't create the log file, just continue without logging
                _logFilePath = string.Empty;
            }
        }

        /// <summary>
        /// Logs an informational message to the update log
        /// </summary>
        public static void LogInfo(string message)
        {
            Log("INFO", message);
        }

        /// <summary>
        /// Logs a warning message to the update log
        /// </summary>
        public static void LogWarning(string message)
        {
            Log("WARNING", message);
        }

        /// <summary>
        /// Logs an error message and exception details to the update log
        /// </summary>
        public static void LogError(string message, Exception? ex = null)


        {
            if (ex != null)
            {
                Log("ERROR", $"{message} - {ex.Message}");
                Log("EXCEPTION", $"Type: {ex.GetType().Name}\r\nSource: {ex.Source}\r\nStack Trace:\r\n{ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Log("INNER EXCEPTION", $"{ex.InnerException.Message}\r\n{ex.InnerException.StackTrace}");
                }
            }
            else
            {
                Log("ERROR", message);
            }
        }

        /// <summary>
        /// Writes a log entry with timestamp and category
        /// </summary>
        private static void Log(string category, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_logFilePath))
                {
                    return;
                }

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}\r\n";

                lock (_logLock)
                {
                    // Store in current session log
                    _currentSessionLog.Append(logEntry);

                    // Write to file
                    File.AppendAllText(_logFilePath, logEntry);
                }
            }
            catch
            {
                // If logging fails, just continue
            }
        }

        /// <summary>
        /// Gets the full path to the current log file
        /// </summary>
        public static string GetLogFilePath()
        {
            return _logFilePath;
        }

        /// <summary>
        /// Gets the contents of the current session log
        /// </summary>
        public static string GetCurrentSessionLog()
        {
            lock (_logLock)
            {
                return _currentSessionLog.ToString();
            }
        }
    }
}