using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SihyuPOSPayroll.ViewModels;

namespace SihyuPOSPayroll
{
    public partial class MainWindow : Window
    {
        private const string EmailPlaceholder = "Email";
        private static readonly Brush PlaceholderBrush = Brushes.Gray;
        private static readonly Brush InputBrush = Brushes.Black;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new LoginViewModel();
        }

        private void Input_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Text == EmailPlaceholder)
            {
                textBox.Text = string.Empty;
                textBox.Foreground = InputBrush;
            }
        }

        private void Input_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = EmailPlaceholder;
                textBox.Foreground = PlaceholderBrush;
            }
        }

        // email text change handling
        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && DataContext is LoginViewModel viewModel)
            {
                // Only update the viewmodel if the text is not the placeholder and we're not in placeholder mode
                if (textBox.Foreground == InputBrush && textBox.Text != EmailPlaceholder)
                {
                    viewModel.Email = textBox.Text;
                }
                else if (textBox.Foreground == PlaceholderBrush)
                {
                    viewModel.Email = string.Empty;
                }
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility =
                string.IsNullOrEmpty(PasswordBox.Password) ? Visibility.Visible : Visibility.Hidden;

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
    }
}