using System;
using System.Collections.Generic;
using InteractiveWorldMap.Services;

namespace InteractiveWorldMap.Tests.TestHelpers;

/// <summary>
/// In-memory logger for unit tests.
/// </summary>
public class MockLogger : ILogger
{
    public List<string> InfoMessages { get; } = new();
    public List<string> WarningMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();

    public void LogError(string message, Exception? ex = null)
    {
        ErrorMessages.Add(ex == null ? message : $"{message} | {ex.Message}");
    }

    public void LogWarning(string message) => WarningMessages.Add(message);

    public void LogInfo(string message) => InfoMessages.Add(message);
}
