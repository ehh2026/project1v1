using System;
using System.IO;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// File-based logger implementation that writes to %APPDATA%/InteractiveWorldMap/logs/app.log
    /// </summary>
    public class FileLogger : ILogger, IDisposable
    {
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();
        private StreamWriter? _writer;

        public FileLogger()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logDirectory = Path.Combine(appDataPath, "InteractiveWorldMap", "logs");
            
            Directory.CreateDirectory(logDirectory);
            
            _logFilePath = Path.Combine(logDirectory, "app.log");
            InitializeWriter();
        }

        private void InitializeWriter()
        {
            try
            {
                _writer = new StreamWriter(_logFilePath, append: true)
                {
                    AutoFlush = true
                };
            }
            catch (Exception ex)
            {
                // If we can't create the log file, write to console in debug builds
                System.Diagnostics.Debug.WriteLine($"Failed to initialize log file: {ex.Message}");
            }
        }

        public void LogError(string message, Exception? ex = null)
        {
            var logMessage = ex != null 
                ? $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message} | Exception: {ex.Message}\n{ex.StackTrace}"
                : $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            
            WriteLog(logMessage);
        }

        public void LogWarning(string message)
        {
            var logMessage = $"[WARNING] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            WriteLog(logMessage);
        }

        public void LogInfo(string message)
        {
            var logMessage = $"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            WriteLog(logMessage);
        }

        private void WriteLog(string message)
        {
            lock (_lockObject)
            {
                try
                {
                    // Write to file
                    _writer?.WriteLine(message);
                    
                    // Write to console
                    Console.WriteLine(message);
                    
                    // Also write to debug output in debug builds
                    System.Diagnostics.Debug.WriteLine(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write log: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Failed to write log: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            lock (_lockObject)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }
    }
}
