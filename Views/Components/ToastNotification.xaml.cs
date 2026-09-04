using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SihyuPOSPayroll.Views.Components
{
    public enum ToastKind { Success, Error, Info, Warning }

    public partial class ToastNotification : UserControl
    {
        private DispatcherTimer? _timer;
        private Storyboard? _showAnim;
        private Storyboard? _hideAnim;

        public ToastNotification()
        {
            InitializeComponent();
            _showAnim = (Storyboard)Resources["ShowAnim"];
            _hideAnim = (Storyboard)Resources["HideAnim"];
            Visibility = Visibility.Collapsed;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Shows the toast for <paramref name="durationMs"/> ms then hides it.</summary>
        public void Show(string message, ToastKind kind = ToastKind.Success, int durationMs = 2800)
        {
            ApplyKind(kind);
            MessageText.Text = message;

            _timer?.Stop();

            Visibility = Visibility.Visible;
            _showAnim?.Begin();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
            _timer.Tick += (_, __) =>
            {
                _timer.Stop();
                _hideAnim?.Begin();
            };
            _timer.Start();
        }

        public void Hide()
        {
            _timer?.Stop();
            _hideAnim?.Begin();
        }

        // ── Storyboard callback ───────────────────────────────────────────────
        private void HideAnim_Completed(object? sender, EventArgs e)
            => Visibility = Visibility.Collapsed;

        // ── Visual variants ───────────────────────────────────────────────────
        private void ApplyKind(ToastKind kind)
        {
            switch (kind)
            {
                case ToastKind.Success:
                    RootBorder.Background = (Brush)new BrushConverter().ConvertFrom("#0D2818")!;
                    RootBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#166534")!;
                    RootBorder.BorderThickness = new Thickness(1);
                    IconText.Text = "\uE73E";            // Segoe MDL2: checkmark
                    IconText.Foreground = (Brush)new BrushConverter().ConvertFrom("#4ADE80")!;
                    break;

                case ToastKind.Error:
                    RootBorder.Background = (Brush)new BrushConverter().ConvertFrom("#2D0A0A")!;
                    RootBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#7F1D1D")!;
                    RootBorder.BorderThickness = new Thickness(1);
                    IconText.Text = "\uEA39";            // Segoe MDL2: error badge
                    IconText.Foreground = (Brush)new BrushConverter().ConvertFrom("#F87171")!;
                    break;

                case ToastKind.Warning:
                    RootBorder.Background = (Brush)new BrushConverter().ConvertFrom("#2D1A00")!;
                    RootBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#78350F")!;
                    RootBorder.BorderThickness = new Thickness(1);
                    IconText.Text = "\uE7BA";            // Segoe MDL2: warning
                    IconText.Foreground = (Brush)new BrushConverter().ConvertFrom("#FCD34D")!;
                    break;

                default: // Info
                    RootBorder.Background = (Brush)new BrushConverter().ConvertFrom("#0A1628")!;
                    RootBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#1E3A5F")!;
                    RootBorder.BorderThickness = new Thickness(1);
                    IconText.Text = "\uE946";            // Segoe MDL2: info
                    IconText.Foreground = (Brush)new BrushConverter().ConvertFrom("#60A5FA")!;
                    break;
            }
        }
    }
}
