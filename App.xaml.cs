using System;
using System.Windows;
using System.Windows.Threading;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ── Global exception handlers — show crash details instead of silent exit ──
            DispatcherUnhandledException += (_, ex) =>
            {
                MessageBox.Show(
                    $"Unhandled UI exception:\n\n{ex.Exception.GetType().Name}: {ex.Exception.Message}\n\n{ex.Exception.StackTrace}",
                    "Crash Report", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true; // keep app alive so you can read the message
            };

            AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            {
                var msg = ex.ExceptionObject is Exception err
                    ? $"{err.GetType().Name}: {err.Message}\n\n{err.StackTrace}"
                    : ex.ExceptionObject?.ToString() ?? "Unknown";
                MessageBox.Show($"Unhandled background exception:\n\n{msg}",
                    "Crash Report", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            // ── SQLite: Users / Auth schema ───────────────────────────────────
            AuthSchemaInitializer.EnsureSchemaAtStartup();

            // ── Settings (SQLite) ─────────────────────────────────────────────
            SettingsService.EnsureSchemaAtStartup();
            SettingsService.Instance.Load();

            // ── PayslipService schema (MySQL — catches its own errors) ─────────
            try { PayslipService.EnsureSchemaAtStartup(); }
            catch { /* MySQL not running — payslip features unavailable */ }
        }
    }
}
