using System.Windows;
using SihyuPOSPayroll.Services;

namespace SihyuPOSPayroll
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Ensure the PayslipRequests and Payslips tables exist with
            // the correct PascalCase names before any view tries to query them.
            PayslipService.EnsureSchemaAtStartup();
        }
    }
}
