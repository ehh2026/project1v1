using System;
using System.Runtime.InteropServices;
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

        protected override void OnStartup(StartupEventArgs e)
        {
            // Allocate a console for debug output
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
            {
                AllocConsole();
            }

            var logger = new FileLogger();
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
