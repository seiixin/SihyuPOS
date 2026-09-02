#nullable enable
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
        private bool    _isBusy;

        // ── Bound properties ──────────────────────────────────────────────────

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

        /// <summary>True while the login request is in flight (disables the button).</summary>
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        // ── Commands ──────────────────────────────────────────────────────────

        public ICommand LoginCommand { get; }

        // ── Services ──────────────────────────────────────────────────────────

        /// <summary>
        /// AuthService is the single entry point for authentication.
        /// DatabaseService is kept for the Menu/Inventory methods that have not
        /// yet been migrated to SQLite.
        /// </summary>
        private readonly AuthService _authService;

        // ── Constructor ───────────────────────────────────────────────────────

        public LoginViewModel()
        {
            _authService = new AuthService();
            LoginCommand = new RelayCommand(Login, _ => !IsBusy);
        }

        // ── Login logic ───────────────────────────────────────────────────────

        private void Login(object? parameter)
        {
            ErrorMessage = string.Empty;

            var emailInput    = Email?.Trim()  ?? string.Empty;
            var passwordInput = Password       ?? string.Empty;

            if (string.IsNullOrWhiteSpace(emailInput) || string.IsNullOrWhiteSpace(passwordInput))
            {
                ErrorMessage = "Please enter your email and password.";
                return;
            }

            IsBusy = true;

            try
            {
                // If a previous session is still in memory (e.g. the user hit Back
                // from a future logout screen), invalidate it before creating a new one.
                if (Session.IsLoggedIn)
                    _authService.Logout();

                // AuthService.Login:
                //   1. Verifies email + BCrypt password against SQLite users table.
                //   2. Writes a row to login_sessions.
                //   3. Populates Session.CurrentUser / CurrentUserId / CurrentUserRole / ActiveToken.
                var user = _authService.Login(emailInput, passwordInput);

                if (user != null)
                {
                    OpenDashboard(user, parameter);
                }
                else
                {
                    ErrorMessage = "Invalid email or password.";
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenDashboard(UserModel user, object? loginWindowParam)
        {
            var mainLayout = new MainLayout
            {
                DataContext = new SidebarViewModel(user)
            };

            // Load the app logo for the window icon
            var logoUri   = new Uri("pack://application:,,,/assets/SihyuPOS-Logo.jpg", UriKind.Absolute);
            var logoImage = new System.Windows.Media.Imaging.BitmapImage(logoUri);

            var window = new Window
            {
                Title                  = $"Dashboard - SihyuPOS  [{user.Role}]",
                Content                = mainLayout,
                Width                  = 1024,
                Height                 = 768,
                WindowStartupLocation  = WindowStartupLocation.CenterScreen,
                WindowState            = WindowState.Maximized,
                Icon                   = logoImage,
                Background             = System.Windows.Media.Brushes.Black,
            };

            // Apply dark title bar via Win32 DwmSetWindowAttribute (DWMWA_USE_IMMERSIVE_DARK_MODE = 20)
            window.SourceInitialized += (_, __) =>
            {
                var hwnd     = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                int darkMode = 1;
                DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));
            };

            // Stamp the session as logged-out if the user simply closes the dashboard window
            // (covers the case where logout is not triggered through the sidebar button).
            window.Closed += (_, __) =>
            {
                if (Session.IsLoggedIn)
                    _authService.Logout();
            };

            window.Show();

            // Close the login window
            if (loginWindowParam is Window loginWindow)
                loginWindow.Close();

            System.Diagnostics.Debug.WriteLine(
                $"[Login] OK — userId={user.Id}, role={user.Role}, " +
                $"token={Session.ActiveToken?[..8]}…");
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
