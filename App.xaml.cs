using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using InteractiveWorldMap.Services;

namespace InteractiveWorldMap
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);

        private const int ATTACH_PARENT_PROCESS = -1;

        // Kept for the lifetime of the process so the crash handlers below can log after startup has
        // returned. FileLogger's writer is process-wide static, so holding an instance is cheap.
        private FileLogger? _crashLogger;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Allocate a console for debug output
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
            {
                AllocConsole();
            }

            var logger = new FileLogger();
            _crashLogger = logger;
            InstallCrashHandlers();

            try
            {
                logger.LogInfo("========================================");
                logger.LogInfo("=== APPLICATION STARTUP ===");
                logger.LogInfo($"Timestamp: {DateTime.Now}");
                logger.LogInfo($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
                logger.LogInfo("========================================");

                base.OnStartup(e);

                logger.LogInfo("Base.OnStartup completed");
            }
            catch (Exception ex)
            {
                logger.LogError($"FATAL ERROR during application startup: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Fatal error during startup:\n{ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        /// <summary>
        /// Without these, an exception that escapes a UI event handler ends the process with nothing
        /// written down: the window disappears and app.log simply stops mid-action, which is the
        /// worst thing that can happen when the report comes from a gallery machine nobody can
        /// attach a debugger to.
        ///
        /// The dispatcher case is marked handled so a visitor is left with a working map rather than
        /// a closed application. That is a deliberate trade — the app may be left in an odd visual
        /// state — and it is only defensible because the failure is now always logged.
        /// </summary>
        private void InstallCrashHandlers()
        {
            DispatcherUnhandledException += (_, args) =>
            {
                LogCrash("Unhandled UI exception (recovered, application left running)", args.Exception);
                args.Handled = true;
            };

            // Cannot be prevented — the process is going down either way — so this exists purely to
            // leave a record of why. It writes to disk on this thread rather than only queueing:
            // the normal path hands the line to a background writer the runtime abandons during
            // shutdown, which would lose the record on precisely the crashes this handler is for.
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogCrash("Unhandled exception (application terminating)", ex);
                FileLogger.WriteTerminatingRecord(
                    $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - " +
                    $"Unhandled exception (application terminating): {ex?.Message}\n{ex?.StackTrace}");
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                LogCrash("Unobserved background task exception", args.Exception);
                args.SetObserved();
            };
        }

        private void LogCrash(string headline, Exception? ex)
        {
            try
            {
                _crashLogger?.LogError($"{headline}: {ex?.Message}\n{ex?.StackTrace}");
            }
            catch
            {
                // A crash handler that throws would replace the original failure with a worse one.
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var logger = new FileLogger();
            logger.LogInfo("========================================");
            logger.LogInfo("=== APPLICATION EXIT ===");
            logger.LogInfo($"Exit Code: {e.ApplicationExitCode}");
            logger.LogInfo("========================================");
            base.OnExit(e);
        }
    }
}
