using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Oracle_Lite
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly string CrashLogPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory,
            "crash.log");

        /// <summary>
        /// Catches otherwise-unhandled exceptions from all three .NET sources
        /// (UI thread, background threads, unobserved Task faults). Without
        /// this, a bug nobody wrapped in try/catch either shows Windows' raw
        /// "stopped working" crash dialog or just silently vanishes - neither
        /// leaves a trail to diagnose a report like "it crashed, no idea why"
        /// after the fact.
        /// </summary>
        private void App_Startup(object sender, StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += App_DomainUnhandledException;
            TaskScheduler.UnobservedTaskException += App_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception, "UI thread");

            // Intentionally the native MessageBox here, not Custom_MessageBox:
            // this fires from a genuinely broken app state, and the custom
            // dialog lives in the same (possibly corrupted) visual tree that
            // may have just crashed - the native OS dialog doesn't depend on
            // any of that and is the safer choice for a last-resort handler.
            MessageBox.Show(
                $"Something went wrong and the launcher needs to close.\n\n{e.Exception.Message}\n\nA log was saved to:\n{CrashLogPath}",
                "Frostworn Launcher - Unexpected Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
            Shutdown();
        }

        private void App_DomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // By the time this fires the process is already terminating
            // (IsTerminating is almost always true here) - too late to safely
            // show UI, just make sure the crash is on disk before it dies.
            LogCrash(e.ExceptionObject as Exception, "background thread");
        }

        private void App_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogCrash(e.Exception, "unobserved Task");
            e.SetObserved(); // don't let a faulted background Task crash the finalizer thread
        }

        private static void LogCrash(Exception ex, string source)
        {
            try
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ({source}) {ex}\r\n\r\n";
                File.AppendAllText(CrashLogPath, entry);
            }
            catch
            {
                // If we can't even write the crash log, there's nothing more we can do here.
            }
        }
    }
}
