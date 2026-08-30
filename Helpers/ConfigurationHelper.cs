using Microsoft.Extensions.Configuration;
using System.IO;

namespace SihyuPOSPayroll.Helpers
{
    public static class ConfigurationHelper
    {
        private static IConfiguration? _configuration;

        public static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    // AppContext.BaseDirectory always points to the exe directory,
                    // regardless of how the app was launched (F5, double-click, etc.).
                    // Directory.GetCurrentDirectory() can vary and is unreliable for WPF apps.
                    var builder = new ConfigurationBuilder()
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

                    _configuration = builder.Build();
                }

                return _configuration;
            }
        }

        public static string GetConnectionString(string name = "DefaultConnection") =>
            Configuration.GetConnectionString(name) ?? string.Empty;
    }
}
