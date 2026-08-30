using System.Windows;
using SihyuPOSPayroll.Data;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ── SQLite: Users / Auth schema ───────────────────────────────────
            // Must run first — other services may check session state at startup.
            // Creates: roles, users, login_sessions tables + seeds default admin.
            AuthSchemaInitializer.EnsureSchemaAtStartup();

            // ── MySQL: Settings (not yet migrated) ────────────────────────────
            // Ensure the Settings tables exist and seed defaults before any
            // other schema migration runs, so settings are available at startup.
            SettingsService.EnsureSchemaAtStartup();

            // Ensure the PayslipRequests and Payslips tables exist with
            // the correct PascalCase names before any view tries to query them.
            PayslipService.EnsureSchemaAtStartup();

            // Load persisted settings into memory after all schema migrations,
            // so CurrentMode and ModuleVisibility are ready before SidebarViewModel
            // is constructed.
            SettingsService.Instance.Load();
        }
    }
}
