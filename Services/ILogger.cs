using System;

namespace InteractiveWorldMap.Services
{
    /// <summary>
    /// Interface for logging application events, errors, and warnings
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs an error message with optional exception details
        /// </summary>
        void LogError(string message, Exception? ex = null);

        /// <summary>
        /// Logs a warning message
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// Logs an informational message
        /// </summary>
        void LogInfo(string message);
    }
}
