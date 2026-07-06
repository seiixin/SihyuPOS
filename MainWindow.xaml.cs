using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll
{
    public partial class MainWindow : Window
    {
        private const string EmailPlaceholder = "admin@sihyupos.com";
        private static readonly Brush PlaceholderBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // #6B7280
        private static readonly Brush InputBrush = new SolidColorBrush(Color.FromRgb(243, 244, 246)); // #F3F4F6
        private bool _isPasswordVisible = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new LoginViewModel();
            EmailPlaceholderText.Text = EmailPlaceholder;
            EmailPlaceholderText.Visibility = Visibility.Visible;
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