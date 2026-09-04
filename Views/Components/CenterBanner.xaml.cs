using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SihyuPOSPayroll.Views.Components
{
    public enum BannerKind { Success, Error, Warning }

    public partial class CenterBanner : System.Windows.Controls.UserControl
    {
        private DispatcherTimer? _timer;
        private Storyboard? _showAnim;
        private Storyboard? _hideAnim;

        public CenterBanner()
        {
            InitializeComponent();
            _showAnim = (Storyboard)Resources["ShowAnim"];
            _hideAnim = (Storyboard)Resources["HideAnim"];
            Visibility = Visibility.Collapsed;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the centered banner for <paramref name="durationMs"/> ms then auto-dismisses.
        /// </summary>
        public void Show(string title, string? sub = null,
                         BannerKind kind = BannerKind.Success, int durationMs = 2200)
        {
            ApplyKind(kind);
            TitleText.Text = title;

            if (!string.IsNullOrWhiteSpace(sub))
            {
                SubText.Text       = sub;
                SubText.Visibility = Visibility.Visible;
            }
            else
            {
                SubText.Visibility = Visibility.Collapsed;
            }

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

        // ── Storyboard completion ─────────────────────────────────────────────
        private void HideAnim_Completed(object? sender, EventArgs e)
            => Visibility = Visibility.Collapsed;

        // ── Visual variants ───────────────────────────────────────────────────
        private void ApplyKind(BannerKind kind)
        {
            switch (kind)
            {
                case BannerKind.Success:
                    RootBorder.Background  = (Brush)new BrushConverter().ConvertFrom("#0D1F14")!;
                    RootBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#166534")!;
                    RootBorder.BorderThickness = new Thickness(1);
                    IconBadge.Background   = (Brush)new BrushConverter().ConvertFrom("#052E16")!;
                    IconText.Text          = "\uE73E";   // Segoe MDL2: checkmark
                    IconText.Foreground    = (Brush)new BrushConverter().ConvertFrom("#4ADE80")!;
                    break;

                case BannerKind.Error:
                    RootBorder.Background  = (Brush)new BrushConverter().ConvertFrom("#1A0808")!;
                    RootBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#7F1D1D")!;
                    RootBorder.BorderThickness = new Thickness(1);
                    IconBadge.Background   = (Brush)new BrushConverter().ConvertFrom("#2D0A0A")!;
                    IconText.Text          = "\uEA39";   // Segoe MDL2: error badge
                    IconText.Foreground    = (Brush)new BrushConverter().ConvertFrom("#F87171")!;
                    break;

                case BannerKind.Warning:
                    RootBorder.Background  = (Brush)new BrushConverter().ConvertFrom("#1A1000")!;
                    RootBorder.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#78350F")!;
                    RootBorder.BorderThickness = new Thickness(1);
                    IconBadge.Background   = (Brush)new BrushConverter().ConvertFrom("#2D1A00")!;
                    IconText.Text          = "\uE7BA";   // Segoe MDL2: warning
                    IconText.Foreground    = (Brush)new BrushConverter().ConvertFrom("#FCD34D")!;
                    break;
            }
        }
    }
}
