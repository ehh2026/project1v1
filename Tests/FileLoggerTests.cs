using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InteractiveWorldMap.Services;
using Xunit;

namespace InteractiveWorldMap.Tests;

[Collection("FileLogger")]
public class FileLoggerTests
{
    [Fact]
    public async Task LogInfo_WritesMessageToInjectedLogFile()
    {
        var tempDir = NewTempDir();
        var logPath = Path.Combine(tempDir, "app.log");

        try
        {
            using (var logger = new FileLogger(new TestLogPathProvider(logPath)))
            {
                logger.LogInfo("hello info");
            }

            var lines = await WaitForLines(logPath, 1);
            Assert.Contains(lines, line => line.Contains("[INFO]") && line.Contains("hello info"));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task LogWarning_WritesWarningLevel()
    {
        var tempDir = NewTempDir();
        var logPath = Path.Combine(tempDir, "app.log");

        try
        {
            using (var logger = new FileLogger(new TestLogPathProvider(logPath)))
            {
                logger.LogWarning("watch out");
            }

            var lines = await WaitForLines(logPath, 1);
            Assert.Contains(lines, line => line.Contains("[WARN]") && line.Contains("watch out"));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task LogError_WithException_WritesExceptionMessage()
    {
        var tempDir = NewTempDir();
        var logPath = Path.Combine(tempDir, "app.log");

        try
        {
            using (var logger = new FileLogger(new TestLogPathProvider(logPath)))
            {
                logger.LogError("failed", new InvalidOperationException("bad state"));
            }

            var lines = await WaitForLines(logPath, 1);
            Assert.Contains(lines, line =>
                line.Contains("[ERROR]") &&
                line.Contains("failed") &&
                line.Contains("bad state"));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task Dispose_WhenLastInstance_CompletesWriter()
    {
        var tempDir = NewTempDir();
        var logPath = Path.Combine(tempDir, "app.log");

        try
        {
            using (var logger = new FileLogger(new TestLogPathProvider(logPath)))
            {
                logger.LogInfo("first lifecycle");
            }

            using (var logger = new FileLogger(new TestLogPathProvider(logPath)))
            {
                logger.LogInfo("second lifecycle");
            }

            var lines = await WaitForLines(logPath, 2);
            Assert.Contains(lines, line => line.Contains("first lifecycle"));
            Assert.Contains(lines, line => line.Contains("second lifecycle"));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void Constructor_CreatesLogDirectory()
    {
        var tempDir = NewTempDir();
        var logPath = Path.Combine(tempDir, "nested", "app.log");

        try
        {
            using var logger = new FileLogger(new TestLogPathProvider(logPath));

            Assert.True(Directory.Exists(Path.GetDirectoryName(logPath)!));
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public void Constructor_WhenDirectoryCreationFails_DoesNotThrow()
    {
        var tempDir = NewTempDir();
        Directory.CreateDirectory(tempDir);
        var blocker = Path.Combine(tempDir, "blocker");
        File.WriteAllText(blocker, "not a directory");
        var logPath = Path.Combine(blocker, "app.log");

        try
        {
            var exception = Record.Exception(() =>
            {
                using var logger = new FileLogger(new TestLogPathProvider(logPath));
            });

            Assert.Null(exception);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    private static string NewTempDir()
        => Path.Combine(Path.GetTempPath(), "FileLogger_" + Guid.NewGuid().ToString("N"));

    private static void DeleteTempDir(string tempDir)
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }

    private static async Task<string[]> WaitForLines(string logPath, int minCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(logPath))
            {
                var lines = File.ReadAllLines(logPath);
                if (lines.Length >= minCount)
                    return lines;
            }

            await Task.Delay(25);
        }

        return File.Exists(logPath)
            ? File.ReadAllLines(logPath)
            : Array.Empty<string>();
    }

    private sealed class TestLogPathProvider : ILogPathProvider
    {
        public TestLogPathProvider(string logFilePath)
        {
            LogFilePath = logFilePath;
        }

        public string LogFilePath { get; }
    }
}

[CollectionDefinition("FileLogger", DisableParallelization = true)]
public sealed class FileLoggerCollection
{
}
