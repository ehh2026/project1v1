using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Non-blocking logger that queues messages and writes on a background thread.
    /// </summary>
    public class FileLogger : ILogger, IDisposable
    {
        private static readonly BlockingCollection<string> _queue = new BlockingCollection<string>(boundedCapacity: 2000);
        private static Thread? _writerThread;
        private static string? _logFilePath;
        private static int _instanceCount = 0;
        private static readonly object _initLock = new object();

        public FileLogger()
        {
            lock (_initLock)
            {
                _instanceCount++;
                if (_writerThread == null)
                    Initialize();
            }
        }

        private static void Initialize()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var logDir = Path.Combine(appData, "InteractiveWorldMap", "logs");

            try { Directory.CreateDirectory(logDir); } catch { }

            _logFilePath = Path.Combine(logDir, "app.log");
            Console.WriteLine($"Log file path: {_logFilePath}");

            _writerThread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "LogWriter"
            };
            _writerThread.Start();
            Console.WriteLine("Log writer initialized successfully");
        }

        private static void WriterLoop()
        {
            try
            {
                using var writer = new StreamWriter(_logFilePath!, append: true) { AutoFlush = false };
                foreach (var message in _queue.GetConsumingEnumerable())
                {
                    // Console/Debug output happens here on the background thread, not on the
                    // (often UI) thread that logged — so logging never blocks the caller.
                    Console.WriteLine(message);
                    System.Diagnostics.Debug.WriteLine(message);

                    writer.WriteLine(message);
                    // Flush only when queue is momentarily empty (batches writes)
                    if (_queue.Count == 0)
                        writer.Flush();
                }
                writer.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log writer thread failed: {ex.Message}");
            }
        }

        public void LogError(string message, Exception? ex = null)
        {
            var msg = ex != null
                ? $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message} | {ex.Message}"
                : $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
            WriteLog(msg);
        }

        public void LogWarning(string message) =>
            WriteLog($"[WARN]  {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}");

        public void LogInfo(string message) =>
            WriteLog($"[INFO]  {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}");

        private static void WriteLog(string message)
        {
            // Hot path: enqueue only. The background WriterLoop does the file + console/debug
            // writes, so logging never blocks the calling (often UI) thread during animation.
            _queue.TryAdd(message); // drops if queue is full (shouldn't happen)
        }

        public void Dispose()
        {
            lock (_initLock)
            {
                _instanceCount--;
                if (_instanceCount == 0)
                    _queue.CompleteAdding();
            }
        }
    }
}
