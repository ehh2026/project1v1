using System;
using System.IO;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// File-based logger implementation that writes to %APPDATA%/InteractiveWorldMap/logs/app.log
    /// Uses a shared file writer to avoid file locking issues.
    /// </summary>
    public class FileLogger : ILogger, IDisposable
    {
        private static readonly object _globalLockObject = new object();
        private static StreamWriter? _sharedWriter;
        private static int _instanceCount = 0;
        private static string? _logFilePath;

        public FileLogger()
        {
            lock (_globalLockObject)
            {
                _instanceCount++;
                
                if (_sharedWriter == null)
                {
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var logDirectory = Path.Combine(appDataPath, "InteractiveWorldMap", "logs");
                    
                    try
                    {
                        Directory.CreateDirectory(logDirectory);
                        _logFilePath = Path.Combine(logDirectory, "app.log");
                        
                        Console.WriteLine($"Log file path: {_logFilePath}");
                        
                        InitializeSharedWriter();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to initialize logger: {ex.Message}");
                        _logFilePath = Path.Combine(Path.GetTempPath(), "InteractiveWorldMap_app.log");
                        Console.WriteLine($"Using fallback log path: {_logFilePath}");
                        InitializeSharedWriter();
                    }
                }
            }
        }

        private static void InitializeSharedWriter()
        {
            try
            {
                _sharedWriter?.Dispose();
                
                _sharedWriter = new StreamWriter(_logFilePath!, append: true)
                {
                    AutoFlush = true
                };
                
                Console.WriteLine($"Log writer initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize log file writer: {ex.Message}");
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
            lock (_globalLockObject)
            {
                try
                {
                    // Write to console first (always works)
                    Console.WriteLine(message);
                    
                    // Write to debug output
                    System.Diagnostics.Debug.WriteLine(message);
                    
                    // Write to file if writer is available
                    if (_sharedWriter != null)
                    {
                        _sharedWriter.WriteLine(message);
                        _sharedWriter.Flush(); // Explicit flush to ensure it's written
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write log: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Failed to write log: {ex.Message}");
                    
                    // Try to reinitialize the writer
                    try
                    {
                        InitializeSharedWriter();
                    }
                    catch
                    {
                        // Ignore reinitialization errors
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (_globalLockObject)
            {
                _instanceCount--;
                
                // Only dispose the shared writer when the last instance is disposed
                if (_instanceCount == 0 && _sharedWriter != null)
                {
                    _sharedWriter.Dispose();
                    _sharedWriter = null;
                }
            }
        }
    }
}
