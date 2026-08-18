using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Non-blocking logger that queues messages and writes on a background thread.
    /// Writer state is process-wide/static: a second instance with a different path is ignored
    /// while an active writer is running (a warning is queued). Dispose is idempotent per instance.
    /// </summary>
    public class FileLogger : ILogger, IDisposable
    {
        private static BlockingCollection<string> _queue = new BlockingCollection<string>(boundedCapacity: 2000);
        private static Thread? _writerThread;
        private static string? _logFilePath;
        private static int _instanceCount = 0;
        private static readonly object _initLock = new object();

        private bool _disposed;

        public FileLogger(ILogPathProvider? pathProvider = null)
        {
            lock (_initLock)
            {
                while (_writerThread != null && _queue.IsAddingCompleted)
                    Monitor.Wait(_initLock);

                _instanceCount++;
                var provider = pathProvider ?? DefaultLogPathProvider.Instance;
                if (_writerThread == null)
                {
                    Initialize(provider);
                    return;
                }

                var requestedPath = provider.LogFilePath;
                if (!string.Equals(requestedPath, _logFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    // A writer is already running for another path. Document the ignored request
                    // instead of failing, because the writer state is process-wide.
                    _queue.TryAdd(
                        $"[WARN]  {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - " +
                        $"Log path '{requestedPath}' ignored; active log file is '{_logFilePath}'");
                }
            }
        }

        private static void Initialize(ILogPathProvider pathProvider)
        {
            if (_queue.IsAddingCompleted)
                _queue = new BlockingCollection<string>(boundedCapacity: 2000);

            _logFilePath = pathProvider.LogFilePath;
            var logFilePath = _logFilePath;
            var logDir = Path.GetDirectoryName(logFilePath);

            if (!string.IsNullOrEmpty(logDir))
            {
                try { Directory.CreateDirectory(logDir); } catch { }
            }

            var queue = _queue;
            queue.TryAdd($"[INFO]  {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - Log file path: {logFilePath}");
            _writerThread = new Thread(() => WriterLoop(logFilePath!, queue))
            {
                IsBackground = true,
                Name = "LogWriter"
            };
            _writerThread.Start();
            queue.TryAdd($"[INFO]  {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - Log writer initialized successfully");
        }

        private static void WriterLoop(string logFilePath, BlockingCollection<string> queue)
        {
            try
            {
                using var writer = new StreamWriter(logFilePath, append: true) { AutoFlush = false };
                foreach (var message in queue.GetConsumingEnumerable())
                {
                    // Console/Debug output happens here on the background thread, not on the
                    // (often UI) thread that logged — so logging never blocks the caller.
                    Console.WriteLine(message);
                    System.Diagnostics.Debug.WriteLine(message);

                    writer.WriteLine(message);
                    // Flush only when queue is momentarily empty (batches writes)
                    if (queue.Count == 0)
                        writer.Flush();
                }
                writer.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log writer thread failed: {ex.Message}");
            }
            finally
            {
                // The writer thread — not Dispose — owns clearing shared state, and only once
                // it has actually stopped touching the file. That way a Dispose() whose bounded
                // Join times out can never leave stale state that lets a second writer open the
                // same file while this one is still running.
                lock (_initLock)
                {
                    if (ReferenceEquals(_queue, queue))
                    {
                        _writerThread = null;
                        _logFilePath = null;
                        if (_queue.IsAddingCompleted)
                            _queue = new BlockingCollection<string>(boundedCapacity: 2000);
                    }
                    Monitor.PulseAll(_initLock);
                }
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
            try
            {
                if (!_queue.IsAddingCompleted)
                    _queue.TryAdd(message); // drops if queue is full (shouldn't happen)
            }
            catch (InvalidOperationException)
            {
                // Last instance was disposed while a caller was logging; dropping is consistent
                // with the bounded queue behavior.
            }
        }

        public void Dispose()
        {
            Thread? threadToJoin = null;

            lock (_initLock)
            {
                if (_disposed)
                    return;
                _disposed = true;

                _instanceCount--;
                if (_instanceCount == 0 && !_queue.IsAddingCompleted)
                {
                    _queue.CompleteAdding();
                    threadToJoin = _writerThread;
                }
            }

            // Bounded wait: give the writer a chance to flush and exit so shutdown is orderly,
            // but never hang the disposing thread if it's stuck on blocked file/console I/O.
            // WriterLoop's own finally block clears the shared state when it actually stops, so
            // a timeout here cannot leave stale state that lets a second writer start early.
            threadToJoin?.Join(TimeSpan.FromSeconds(5));
        }

        private sealed class DefaultLogPathProvider : ILogPathProvider
        {
            public static readonly DefaultLogPathProvider Instance = new();

            public string LogFilePath
            {
                get
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    return Path.Combine(appData, "InteractiveWorldMap", "logs", "app.log");
                }
            }
        }
    }
}
