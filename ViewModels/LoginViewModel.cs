using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using SihyuPOSPayroll.Helpers;
using SihyuPOSPayroll.Models;
using SihyuPOSPayroll.Services;
using SihyuPOSPayroll.Views.Layouts;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        // Win32: enable dark title bar on Windows 10/11
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private string? _email;
        private string? _password;
        private string? _errorMessage;

        public string? Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string? Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }

        private readonly DatabaseService _dbService;

        public LoginViewModel()
        {
            _dbService = new DatabaseService();
            LoginCommand = new RelayCommand(Login);
        }

        private void Login(object? parameter)
        {
            ErrorMessage = string.Empty;

            var emailInput = Email?.Trim() ?? string.Empty;
            var passwordInput = Password ?? string.Empty;

            // Authenticate using email and password
            var user = _dbService.AuthenticateUser(emailInput, passwordInput);

            if (user != null)
            {
                string userRole = user.Role;
                string userName = "System User";

                // Load employee details 
                if (user.Employee != null)
                {
                    userName = user.Employee.FullName;
                }

                var mainLayout = new MainLayout
                {
                    DataContext = new SidebarViewModel(user)
                };

                // Load the app logo for the window icon
                var logoUri = new Uri("pack://application:,,,/assets/SihyuPOS-Logo.jpg", UriKind.Absolute);
                var logoImage = new System.Windows.Media.Imaging.BitmapImage(logoUri);

                var window = new Window
                {
                    Title = "Dashboard - SihyuPOS",
                    Content = mainLayout,
                    Width = 1024,
                    Height = 768,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    WindowState = System.Windows.WindowState.Maximized,
                    Icon = logoImage,
                    Background = System.Windows.Media.Brushes.Black,
                };

                // Dark title bar via WindowChrome
                var chrome = new System.Windows.Shell.WindowChrome
                {
                    CaptionHeight = 32,
                    ResizeBorderThickness = new Thickness(5),
                    UseAeroCaptionButtons = true,
                    GlassFrameThickness = new Thickness(0),
                    NonClientFrameEdges = System.Windows.Shell.NonClientFrameEdges.None,
                };
                System.Windows.Shell.WindowChrome.SetWindowChrome(window, chrome);

                // Apply dark title bar via Win32 DwmSetWindowAttribute (DWMWA_USE_IMMERSIVE_DARK_MODE)
                window.SourceInitialized += (_, __) =>
                {
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    int darkMode = 1;
                    DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
                };

                window.Show();

                if (parameter is Window loginWindow)
                {
                    loginWindow.Close();
                }

                Console.WriteLine($"Login success: Email = {emailInput}, Role = {userRole}, Name = {userName}");
            }
            else
            {
                ErrorMessage = "Invalid email or password.";
                Console.WriteLine("Login failed.");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
