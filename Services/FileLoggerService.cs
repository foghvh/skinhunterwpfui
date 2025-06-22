using System.IO;
using System.Runtime.CompilerServices;
using System;

namespace skinhunter.Services
{
    public static class FileLoggerService
    {
        private static readonly string _logFilePath;
        private static readonly object _lock = new();

        static FileLoggerService()
        {
            string logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
            try
            {
                Directory.CreateDirectory(logDirectory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileLoggerService] Failed to create log directory: {logDirectory}. Error: {ex.Message}");
            }
            _logFilePath = Path.Combine(logDirectory, $"skinhunter_debug_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            Log("Logger initialized. Logging to: " + _logFilePath);
        }

        public static void Log(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                lock (_lock)
                {
                    string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}:{sourceLineNumber}] {message}{Environment.NewLine}";
                    File.AppendAllText(_logFilePath, logMessage, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileLoggerService] Failed to write to log file. Error: {ex.Message}");
            }
        }
    }
}