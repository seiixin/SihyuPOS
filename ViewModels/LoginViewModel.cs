using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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

                var window = new Window
                {
                    Title = "Dashboard",
                    Content = mainLayout,
                    Width = 1024,
                    Height = 768,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
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
