using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            StreamWriter? writer = null;

            // Approximate characters written since the last length check. Never larger than the
            // rotation limit itself, so a small limit (as tests use) still gets checked promptly.
            var rotationCheckInterval = Math.Min(1024L * 1024, Math.Max(1, MaxLogBytes));
            var pendingBytes = 0L;

            // Messages consumed while the file could not be opened. Bounded, because the hold could
            // in principle last for the whole session: past the cap the oldest go and the count of
            // what went is written into the log instead, so the gap is visible rather than silent.
            var pending = new Queue<string>();
            var droppedWhileUnavailable = 0;

            try
            {
                // Deliberately the same forgiving open as the rotation path below. Failing here used
                // to end the thread before it consumed anything, which costs the whole session's
                // logging — and the likeliest cause is a second copy of the app started by accident,
                // since StreamWriter holds the file against other writers. Whichever copy loses the
                // race now keeps retrying and starts logging when the other one exits.
                writer = TryOpenLog(logFilePath);

                foreach (var message in queue.GetConsumingEnumerable())
                {
                    // Console/Debug output happens here on the background thread, not on the
                    // (often UI) thread that logged — so logging never blocks the caller.
                    Console.WriteLine(message);
                    System.Diagnostics.Debug.WriteLine(message);

                    // A rotation that could not reopen the file leaves this null. Keep draining the
                    // queue and retry the open on each message instead of ending the thread: the
                    // queue would otherwise stay open with no consumer, silently filling until
                    // every later log line is dropped for the rest of the session.
                    writer ??= TryOpenLog(logFilePath);
                    if (writer == null)
                    {
                        // The file is unavailable, not gone: hold what would otherwise be written
                        // and let the next message try the open again. A lock is usually another
                        // process that will let go, and these are exactly the lines explaining what
                        // this copy of the app was doing while it could not say so.
                        HoldWhileUnavailable(pending, message, ref droppedWhileUnavailable);
                        continue;
                    }

                    if (pending.Count > 0 || droppedWhileUnavailable > 0)
                        WritePending(writer, pending, ref droppedWhileUnavailable);

                    writer.WriteLine(message);

                    // Flush when the queue is momentarily empty (batches writes), and otherwise
                    // once enough text has gone past to be worth measuring. Checking only at the
                    // empty-queue boundary would mean a writer that never catches up never checks
                    // its size at all, so a sustained burst could run past the limit unchecked.
                    pendingBytes += message.Length + Environment.NewLine.Length;
                    if (queue.Count != 0 && pendingBytes < rotationCheckInterval)
                        continue;

                    pendingBytes = 0;
                    writer.Flush();

                    // Rotation lives here because this thread is the only writer of the file:
                    // checking after a flush means the length is accurate, and closing then
                    // reopening cannot race any other producer.
                    if (!LogFileRotator.ShouldRotate(writer.BaseStream.Length, MaxLogBytes))
                        continue;

                    writer.Dispose();
                    writer = null;
                    LogFileRotator.Rotate(logFilePath, LogGenerations);
                    writer = TryOpenLog(logFilePath);
                }

                // Shutdown: one last attempt, since whatever held the file may have let go by now.
                if (pending.Count > 0 || droppedWhileUnavailable > 0)
                {
                    writer ??= TryOpenLog(logFilePath);
                    if (writer != null)
                        WritePending(writer, pending, ref droppedWhileUnavailable);
                }

                writer?.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Log writer thread failed: {ex.Message}");
            }
            finally
            {
                writer?.Dispose();

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

        /// <summary>
        /// Writes <paramref name="message"/> to disk on the calling thread, for the one case where
        /// the normal path cannot work: the process is terminating. <see cref="LogError"/> only
        /// queues, and the writer that drains that queue is a background thread the runtime abandons
        /// during shutdown — so a record of the exception that is killing the app is exactly the
        /// record most likely never to be written.
        ///
        /// It goes to a separate file because the writer thread holds the main log open against
        /// other writers; appending there would fail while it is alive. Best-effort throughout: this
        /// is called from a crash handler, where throwing would replace the failure being recorded.
        /// </summary>
        public static void WriteTerminatingRecord(string message)
        {
            try
            {
                var basePath = _logFilePath ?? DefaultLogPathProvider.Instance.LogFilePath;
                var directory = Path.GetDirectoryName(basePath);
                var fileName = Path.GetFileNameWithoutExtension(basePath) + ".crash.log";
                var crashPath = string.IsNullOrEmpty(directory)
                    ? fileName
                    : Path.Combine(directory, fileName);

                // The unattended launcher restarts the app every few seconds, so a failure
                // that happens on every startup appends here forever. Rotate rather than let
                // a machine left running all weekend fill its disk with one repeated record.
                // This keeps the most recent records only — the first occurrence is lost once
                // two generations have filled, which is a real cost, but a repeating failure
                // repeats its stack trace and the alternative is an unbounded file.
                var existing = new FileInfo(crashPath);
                if (existing.Exists && LogFileRotator.ShouldRotate(existing.Length, MaxCrashBytes))
                    LogFileRotator.Rotate(crashPath, 1);

                // The runtime may run the unhandled-exception handler on several threads
                // at once, and a second copy of the app crashing at the same moment writes
                // here too. Each of those records is the only account of a thread that is
                // dying, so none may be lost. Writers are kept exclusive on purpose:
                // FileMode.Append seeks to the end as it stands at open time, so two
                // writers sharing the file would both write at the same offset and one
                // record would vanish. Losers of the race wait and take their turn.
                for (var attempt = 0; ; attempt++)
                {
                    try
                    {
                        using var stream = new FileStream(
                            crashPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                        using var writer = new StreamWriter(stream);
                        writer.WriteLine(message);
                        break;
                    }
                    catch (IOException) when (attempt < MaxCrashWriteAttempts)
                    {
                        Thread.Sleep(15);
                    }
                }
            }
            catch
            {
                // Ignored: see remarks.
            }
        }

        /// <summary>
        /// How many times a crash record retries a file another writer holds. Deliberately
        /// small: in-process contention clears in microseconds, so the only thing a long
        /// wait buys is a slow outside holder — and waiting in a handler the host may kill
        /// at any moment is itself a way to lose the record. Past this the record is given
        /// up, which is the one case this file cannot cover.
        /// </summary>
        private const int MaxCrashWriteAttempts = 10;

        /// <summary>Size at which app.crash.log rotates. Smaller than the main log: it holds
        /// one line per terminating failure, so anything near this is the same crash repeating.</summary>
        internal static long MaxCrashBytes = 1024L * 1024;

        /// <summary>Maximum lines held while the log file cannot be opened.</summary>
        private const int MaxPendingLines = 2000;

        private static void HoldWhileUnavailable(Queue<string> pending, string message, ref int dropped)
        {
            pending.Enqueue(message);

            while (pending.Count > MaxPendingLines)
            {
                pending.Dequeue();
                dropped++;
            }
        }

        private static void WritePending(StreamWriter writer, Queue<string> pending, ref int dropped)
        {
            if (dropped > 0)
            {
                writer.WriteLine(
                    $"[WARN]  {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - " +
                    $"{dropped} earlier line(s) dropped while the log file was unavailable.");
                dropped = 0;
            }

            while (pending.Count > 0)
                writer.WriteLine(pending.Dequeue());
        }

        private static StreamWriter? TryOpenLog(string logFilePath)
        {
            try
            {
                return new StreamWriter(logFilePath, append: true) { AutoFlush = false };
            }
            catch
            {
                // Another process may hold the file for a moment (a viewer, a backup sweep). The
                // caller retries on the next message rather than treating this as fatal.
                return null;
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
