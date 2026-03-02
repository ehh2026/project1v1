using System;

namespace InteractiveWorldMap.Models
{
    /// <summary>
    /// Event arguments for content loading error events.
    /// </summary>
    public class LoadErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Human-readable error message describing what went wrong.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// The underlying exception that caused the error, if available.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Initializes a new instance of LoadErrorEventArgs.
        /// </summary>
        public LoadErrorEventArgs(string errorMessage, Exception? exception = null)
        {
            ErrorMessage = errorMessage;
            Exception = exception;
        }
    }
}
