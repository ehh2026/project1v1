using System;
using System.IO;
using System.Threading.Tasks;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

public class LogFileRotatorTests
{
    [Theory]
    [InlineData(0, 100, false)]
    [InlineData(99, 100, false)]
    [InlineData(100, 100, true)]
    [InlineData(1000, 100, true)]
    [InlineData(1000, 0, false)]
    public void ShouldRotate_TriggersAtOrAboveLimit(long currentBytes, long maxBytes, bool expected)
        => Assert.Equal(expected, LogFileRotator.ShouldRotate(currentBytes, maxBytes));

    [Fact]
    public void Rotate_MovesLiveFileToFirstGeneration()
    {
        var dir = NewTempDir();
        try
        {
            var logPath = Path.Combine(dir, "app.log");
            File.WriteAllText(logPath, "live");

            LogFileRotator.Rotate(logPath, generations: 3);

            Assert.False(File.Exists(logPath));
            Assert.Equal("live", File.ReadAllText(logPath + ".1"));
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void Rotate_ShiftsGenerationsUpAndDiscardsOldest()
    {
        var dir = NewTempDir();
        try
        {
            var logPath = Path.Combine(dir, "app.log");
            File.WriteAllText(logPath, "live");
            File.WriteAllText(logPath + ".1", "one");
            File.WriteAllText(logPath + ".2", "two");
            File.WriteAllText(logPath + ".3", "three");

            LogFileRotator.Rotate(logPath, generations: 3);

            // "three" was the oldest kept generation, so it is the one that falls off the end.
            Assert.Equal("live", File.ReadAllText(logPath + ".1"));
            Assert.Equal("one", File.ReadAllText(logPath + ".2"));
            Assert.Equal("two", File.ReadAllText(logPath + ".3"));
            Assert.False(File.Exists(logPath + ".4"));
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void Rotate_WithMissingLogFile_DoesNothing()
    {
        var dir = NewTempDir();
        try
        {
            var logPath = Path.Combine(dir, "app.log");

            LogFileRotator.Rotate(logPath, generations: 3);

            Assert.False(File.Exists(logPath));
            Assert.False(File.Exists(logPath + ".1"));
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void Rotate_WithZeroGenerations_LeavesLiveFileAlone()
    {
        var dir = NewTempDir();
        try
        {
            var logPath = Path.Combine(dir, "app.log");
            File.WriteAllText(logPath, "live");

            LogFileRotator.Rotate(logPath, generations: 0);

            Assert.Equal("live", File.ReadAllText(logPath));
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void Rotate_WithBlockedGeneration_KeepsTheGenerationsBelowIt()
    {
        var dir = NewTempDir();
        try
        {
            var logPath = Path.Combine(dir, "app.log");
            File.WriteAllText(logPath, "live");
            File.WriteAllText(logPath + ".1", "one");
            File.WriteAllText(logPath + ".2", "two");
            File.WriteAllText(logPath + ".3", "three");

            // The oldest generation cannot be deleted, so .2 has nowhere to go. Rotation must stop
            // rather than free up .2's slot for a move that will not happen and lose "two".
            using (new FileStream(logPath + ".3", FileMode.Open, FileAccess.Read, FileShare.None))
            {
                LogFileRotator.Rotate(logPath, generations: 3);
            }

            Assert.Equal("live", File.ReadAllText(logPath));
            Assert.Equal("one", File.ReadAllText(logPath + ".1"));
            Assert.Equal("two", File.ReadAllText(logPath + ".2"));
            Assert.Equal("three", File.ReadAllText(logPath + ".3"));
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void Rotate_WithMissingMiddleGeneration_StillShiftsTheRest()
    {
        var dir = NewTempDir();
        try
        {
            var logPath = Path.Combine(dir, "app.log");
            File.WriteAllText(logPath, "live");
            File.WriteAllText(logPath + ".1", "one");
            // No .2 yet — a gap is not a failure.

            LogFileRotator.Rotate(logPath, generations: 3);

            Assert.False(File.Exists(logPath));
            Assert.Equal("live", File.ReadAllText(logPath + ".1"));
            Assert.Equal("one", File.ReadAllText(logPath + ".2"));
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    [Fact]
    public void Rotate_WithLockedLiveFile_DoesNotThrow()
    {
        var dir = NewTempDir();
        try
        {
            var logPath = Path.Combine(dir, "app.log");
            File.WriteAllText(logPath, "live");

            using var held = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // A logger that throws while logging is worse than an oversized log file.
            LogFileRotator.Rotate(logPath, generations: 3);
        }
        finally
        {
            DeleteTempDir(dir);
        }
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "LogRotate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DeleteTempDir(string dir)
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}

[Collection("FileLogger")]
public class FileLoggerRotationTests
{
    [Fact]
    public async Task WriterLoop_RotatesOnceTheLiveFileExceedsTheLimit()
    {
        var dir = Path.Combine(Path.GetTempPath(), "FileLogger_" + Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(dir, "app.log");

        var originalMax = FileLogger.MaxLogBytes;
        var originalGenerations = FileLogger.LogGenerations;
        FileLogger.MaxLogBytes = 512;
        FileLogger.LogGenerations = 2;

        try
        {
            using (var logger = new FileLogger(new RotationLogPathProvider(logPath)))
            {
                // Comfortably past 512 bytes; the writer rotates whenever it drains the queue.
                for (var i = 0; i < 200; i++)
                    logger.LogInfo($"line {i} padded out so the file grows quickly");
            }

            Assert.True(await WaitForFile(logPath + ".1"), "expected a rotated app.log.1");
        }
        finally
        {
            FileLogger.MaxLogBytes = originalMax;
            FileLogger.LogGenerations = originalGenerations;

            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task WriterLoop_KeepsLoggingWhenRotationIsBlocked()
    {
        var dir = Path.Combine(Path.GetTempPath(), "FileLogger_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var logPath = Path.Combine(dir, "app.log");

        var originalMax = FileLogger.MaxLogBytes;
        var originalGenerations = FileLogger.LogGenerations;
        FileLogger.MaxLogBytes = 512;
        FileLogger.LogGenerations = 1;

        FileStream? blocker = null;
        try
        {
            // Hold the only rotation target, so every rotation attempt fails and the live file has
            // to stay open and keep receiving lines instead of the writer thread giving up.
            File.WriteAllText(logPath + ".1", "held");
            blocker = new FileStream(logPath + ".1", FileMode.Open, FileAccess.Read, FileShare.None);

            using (var logger = new FileLogger(new RotationLogPathProvider(logPath)))
            {
                for (var i = 0; i < 200; i++)
                    logger.LogInfo($"line {i} padded out so the file passes the rotation threshold");

                logger.LogInfo("still logging after blocked rotation");
            }

            Assert.True(
                await WaitForContent(logPath, "still logging after blocked rotation"),
                "expected the writer to survive a rotation it could not perform");
        }
        finally
        {
            blocker?.Dispose();
            FileLogger.MaxLogBytes = originalMax;
            FileLogger.LogGenerations = originalGenerations;

            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WriteTerminatingRecord_WritesOnTheCallingThreadWhileTheWriterHoldsTheLog()
    {
        var dir = Path.Combine(Path.GetTempPath(), "FileLogger_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var logPath = Path.Combine(dir, "app.log");

        try
        {
            using (var logger = new FileLogger(new RotationLogPathProvider(logPath)))
            {
                logger.LogInfo("normal line");

                // No waiting and no background writer involved: the process this stands in for is
                // being torn down, so the record has to exist the moment the call returns.
                FileLogger.WriteTerminatingRecord("[ERROR] fatal thing happened");

                var crashPath = Path.Combine(dir, "app.crash.log");
                Assert.True(File.Exists(crashPath), "expected app.crash.log beside the main log");
                Assert.Contains("fatal thing happened", File.ReadAllText(crashPath));
            }
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private static async Task<bool> WaitForContent(string path, string expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                if (reader.ReadToEnd().Contains(expected))
                    return true;
            }

            await Task.Delay(25);
        }

        return false;
    }

    private static async Task<bool> WaitForFile(string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
                return true;

            await Task.Delay(25);
        }

        return File.Exists(path);
    }

    private sealed class RotationLogPathProvider : ILogPathProvider
    {
        public RotationLogPathProvider(string logFilePath) => LogFilePath = logFilePath;

        public string LogFilePath { get; }
    }
}
