using System.IO;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Size-triggered rotation for the append-mode log file. Zoom animations and marker placement
    /// log several lines apiece, so a kiosk left running for months writes a log with no ceiling;
    /// rotation caps total consumption at roughly <c>maxBytes * (generations + 1)</c>.
    ///
    /// Every operation swallows I/O failures. A logger that throws while logging is worse than an
    /// oversized log, and a failed rotation simply leaves the current file in place to be retried at
    /// the next size check.
    /// </summary>
    public static class LogFileRotator
    {
        /// <summary>Rotate once the live file reaches this size.</summary>
        public const long DefaultMaxBytes = 10L * 1024 * 1024;

        /// <summary>How many rotated files to keep (app.log.1 … app.log.N).</summary>
        public const int DefaultGenerations = 3;

        public static bool ShouldRotate(long currentBytes, long maxBytes) =>
            maxBytes > 0 && currentBytes >= maxBytes;

        /// <summary>
        /// Renames <paramref name="logFilePath"/> to <c>.1</c>, shifting existing generations up and
        /// discarding the oldest. The caller must have closed the file first.
        /// </summary>
        public static void Rotate(string logFilePath, int generations)
        {
            if (string.IsNullOrEmpty(logFilePath) || generations < 1)
                return;

            // Oldest first, then shift upward, so no move ever overwrites a file it still needs.
            TryDelete($"{logFilePath}.{generations}");

            // A failed move means the slot above is still occupied, so every later move in this pass
            // would overwrite a generation that has nowhere to go. Stopping keeps what is on disk;
            // continuing would delete a still-wanted file to make room for one that never arrives.
            for (var generation = generations - 1; generation >= 1; generation--)
            {
                if (!TryMove($"{logFilePath}.{generation}", $"{logFilePath}.{generation + 1}"))
                    return;
            }

            TryMove(logFilePath, $"{logFilePath}.1");
        }

        private static bool TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);

                return true;
            }
            catch
            {
                // Ignored: see class remarks.
                return false;
            }
        }

        private static bool TryMove(string source, string destination)
        {
            try
            {
                // Nothing to move is not a failure — that generation simply does not exist yet, and
                // the ones below it can still shift up.
                if (!File.Exists(source))
                    return true;

                // File.Move throws when the destination exists on .NET Framework-era semantics, so
                // clear it first; if that clearing fails there is no point attempting the move.
                if (!TryDelete(destination) || File.Exists(destination))
                    return false;

                File.Move(source, destination);
                return true;
            }
            catch
            {
                // Ignored: see class remarks.
                return false;
            }
        }
    }
}
