using System;
using System.Threading.Tasks;
using System.Windows;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Initialized in OnStartup
        public static DatabaseService DatabaseServices { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            // --- Global crash handlers (show a dialog, don't silently exit) ---
            // UI thread exceptions
            this.DispatcherUnhandledException += (s, ev) =>
            {
                ShowCrash("Unhandled UI exception", ev.Exception);
                ev.Handled = true; // keep app alive after showing the dialog
            };

            // Background / worker thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, ev2) =>
            {
                if (ev2.ExceptionObject is Exception ex)
                    ShowCrash("Unhandled non-UI exception", ex);
            };

            // Task exceptions not observed
            TaskScheduler.UnobservedTaskException += (s, ev3) =>
            {
                ShowCrash("Unobserved task exception", ev3.Exception);
                ev3.SetObserved();
            };

            // --- Initialize services early ---
            DatabaseServices = new DatabaseService();

            base.OnStartup(e);
        }

        private static void ShowCrash(string title, Exception ex)
        {
            var root = ex.InnerException ?? ex;
            MessageBox.Show(
                $"{title}\n\n{root.GetType().Name}: {root.Message}\n\n{root.StackTrace}",
                "SihyuPOSPayroll – Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
}
