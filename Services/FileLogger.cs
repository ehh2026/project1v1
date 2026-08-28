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
        private static readonly TimeSpan WriterShutdownTimeout = TimeSpan.FromSeconds(5);

        // Caps log consumption at roughly 40 MB total. Tests override these through the internal
        // writer settings below rather than by writing tens of megabytes.
        internal static long MaxLogBytes = LogFileRotator.DefaultMaxBytes;
        internal static int LogGenerations = LogFileRotator.DefaultGenerations;

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
                var forcedTakeover = false;
                var deadline = DateTime.UtcNow.Add(WriterShutdownTimeout);
                while (_writerThread != null && _queue.IsAddingCompleted)
                {
                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero || !Monitor.Wait(_initLock, remaining))
                    {
                        // The previous writer hasn't exited within the shutdown window (likely
                        // stuck on blocked I/O). Stop waiting and start a fresh writer/queue
                        // rather than blocking this constructor forever. Initialize() always
                        // allocates a new queue when the old one's IsAddingCompleted, so the
                        // stale writer's finally block (which compares queue identity before
                        // touching shared state) can never clobber what we set up here once it
                        // eventually does exit.
                        forcedTakeover = true;
                        break;
                    }
                }

                _instanceCount++;
                var provider = pathProvider ?? DefaultLogPathProvider.Instance;
                if (_writerThread == null || forcedTakeover)
                {
                    // If the stale writer is still stuck mid-I/O it may still hold its own log
                    // file open, so reusing that same path here could fail to open (sharing
                    // violation) and leave the new queue with no consumer at all. Route the
                    // takeover writer to a distinct recovery path so it can never collide.
                    var logFilePath = forcedTakeover
                        ? BuildRecoveryLogPath(provider.LogFilePath)
                        : provider.LogFilePath;
                    Initialize(logFilePath);
                    if (forcedTakeover)
                    {
                        _queue.TryAdd(
                            $"[WARN]  {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - " +
                            $"Previous log writer did not exit within the shutdown window; " +
                            $"switched to recovery log '{logFilePath}'.");
                    }
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

        private static string BuildRecoveryLogPath(string originalPath)
        {
            var dir = Path.GetDirectoryName(originalPath);
            var name = Path.GetFileNameWithoutExtension(originalPath);
            var ext = Path.GetExtension(originalPath);
            var recoveryName = $"{name}.recovery-{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
            return string.IsNullOrEmpty(dir) ? recoveryName : Path.Combine(dir, recoveryName);
        }

        private static void Initialize(string logFilePath)
        {
            if (_queue.IsAddingCompleted)
                _queue = new BlockingCollection<string>(boundedCapacity: 2000);

            _logFilePath = logFilePath;
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
                var writer = new StreamWriter(logFilePath, append: true) { AutoFlush = false };
                try
                {
                    foreach (var message in queue.GetConsumingEnumerable())
                    {
                        // Console/Debug output happens here on the background thread, not on the
                        // (often UI) thread that logged — so logging never blocks the caller.
                        Console.WriteLine(message);
                        System.Diagnostics.Debug.WriteLine(message);

                        writer.WriteLine(message);
                        // Flush only when queue is momentarily empty (batches writes)
                        if (queue.Count != 0)
                            continue;

                        writer.Flush();

                        // Rotation lives here because this thread is the only writer of the file:
                        // checking after a flush means the length is accurate, and closing then
                        // reopening cannot race any other producer.
                        if (!LogFileRotator.ShouldRotate(writer.BaseStream.Length, MaxLogBytes))
                            continue;

                        writer.Dispose();
                        LogFileRotator.Rotate(logFilePath, LogGenerations);
                        writer = new StreamWriter(logFilePath, append: true) { AutoFlush = false };
                    }

                    writer.Flush();
                }
                finally
                {
                    writer.Dispose();
                }
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
            threadToJoin?.Join(WriterShutdownTimeout);
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
