using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll
{
    public partial class MainWindow : Window
    {
        private const string EmailPlaceholder = "admin@sihyupos.com";
        private static readonly Brush PlaceholderBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128));
        private static readonly Brush InputBrush = new SolidColorBrush(Color.FromRgb(243, 244, 246));
        private bool _isPasswordVisible = false;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new LoginViewModel();
            EmailPlaceholderText.Text = EmailPlaceholder;
            EmailPlaceholderText.Visibility = Visibility.Visible;

            // Set window icon from embedded resource
            try
            {
                var logoUri = new Uri("pack://application:,,,/assets/SihyuPOS-Logo.jpg", UriKind.Absolute);
                Icon = new BitmapImage(logoUri);
            }
            catch { /* fallback: no icon */ }

            // Enable dark title bar
            SourceInitialized += (_, __) =>
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int dark = 1;
                DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            };
        }

        private void Input_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Text == EmailPlaceholder)
            {
                textBox.Text = string.Empty;
                textBox.Foreground = InputBrush;
            }

            if (sender is TextBox && EmailPlaceholderText != null)
            {
                EmailPlaceholderText.Visibility = Visibility.Hidden;
            }
        }

        private void Input_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    textBox.Text = string.Empty;
                    if (EmailPlaceholderText != null)
                    {
                        EmailPlaceholderText.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        // email text change handling
        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && DataContext is LoginViewModel viewModel)
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    if (EmailPlaceholderText != null)
                    {
                        EmailPlaceholderText.Visibility = Visibility.Hidden;
                    }
                    viewModel.Email = textBox.Text;
                }
                else
                {
                    if (EmailPlaceholderText != null)
                    {
                        EmailPlaceholderText.Visibility = Visibility.Visible;
                    }
                    viewModel.Email = string.Empty;
                }
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (PasswordPlaceholder != null)
            {
                PasswordPlaceholder.Visibility =
                    string.IsNullOrEmpty(PasswordBox.Password) ? Visibility.Visible : Visibility.Hidden;
            }

            if (DataContext is LoginViewModel viewModel)
            {
                viewModel.Password = PasswordBox.Password;
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm && vm.LoginCommand.CanExecute(this))
            {
                vm.LoginCommand.Execute(this);
            }
        }

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                _isPasswordVisible = !_isPasswordVisible;
                btn.Content = _isPasswordVisible ? "HIDE" : "SHOW";
                // Note: A true password toggle requires a TextBox to switch to, since PasswordBox doesn't support plain text easily.
                // For this demo, we'll just toggle the text; full functionality would require swapping controls.
            }
        }
    }
}