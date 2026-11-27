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
        private const string EmailPlaceholder = "admin@sariquick.ph";
        private static readonly Brush PlaceholderBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128));
        private static readonly Brush InputBrush       = new SolidColorBrush(Color.FromRgb(26,  26,  46));
        private bool _isPasswordVisible = false;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new LoginViewModel();
            EmailPlaceholderText.Text       = EmailPlaceholder;
            EmailPlaceholderText.Visibility = Visibility.Visible;

            // Load banner image via stream — WPF pack URI rejects .jfif by extension
            try
            {
                var sri = Application.GetResourceStream(
                    new Uri("pack://application:,,,/assets/SariQuick-Banner.jfif"));
                if (sri != null)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = sri.Stream;
                    bmp.CacheOption  = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    BannerImage.Source = bmp;
                }
            }
            catch { /* banner missing — panel stays solid colour */ }

            // Window icon
            try
            {
                Icon = new BitmapImage(
                    new Uri("pack://application:,,,/assets/SihyuPOS-Logo.png", UriKind.Absolute));
            }
            catch { /* no icon fallback */ }

            // Light title bar
            SourceInitialized += (_, __) =>
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int dark = 0;
                DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            };
        }

        private void Input_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Text == EmailPlaceholder)
            {
                tb.Text       = string.Empty;
                tb.Foreground = InputBrush;
            }
            if (EmailPlaceholderText != null)
                EmailPlaceholderText.Visibility = Visibility.Hidden;
        }

        private void Input_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = string.Empty;
                if (EmailPlaceholderText != null)
                    EmailPlaceholderText.Visibility = Visibility.Visible;
            }
        }

        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (DataContext is not LoginViewModel vm) return;

            bool hasText = !string.IsNullOrWhiteSpace(tb.Text);
            if (EmailPlaceholderText != null)
                EmailPlaceholderText.Visibility = hasText ? Visibility.Hidden : Visibility.Visible;
            vm.Email = hasText ? tb.Text : string.Empty;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (PasswordPlaceholder != null)
                PasswordPlaceholder.Visibility =
                    string.IsNullOrEmpty(PasswordBox.Password) ? Visibility.Visible : Visibility.Hidden;

            if (DataContext is LoginViewModel vm)
                vm.Password = PasswordBox.Password;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm && vm.LoginCommand.CanExecute(this))
                vm.LoginCommand.Execute(this);

            if (DataContext is LoginViewModel loginVm)
            {
                bool hasError = !string.IsNullOrWhiteSpace(loginVm.ErrorMessage);
                ErrorBanner.Visibility = hasError ? Visibility.Visible : Visibility.Collapsed;
                if (hasError) ErrorText.Text = loginVm.ErrorMessage;
            }
        }

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;
            EyeIcon.Text = _isPasswordVisible ? "\uED1A" : "\uE7B3";
        }
    }
}
