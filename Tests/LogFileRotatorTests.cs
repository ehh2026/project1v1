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
