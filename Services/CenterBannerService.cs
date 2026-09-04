using SihyuPOSPayroll.Views.Components;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Static message-bus for the <see cref="CenterBanner"/> overlay.
    ///
    /// Usage:
    ///   Page registers:  CenterBannerService.Register(myCenterBannerControl);
    ///   ViewModel calls: CenterBannerService.Success("Item saved!");
    /// </summary>
    public static class CenterBannerService
    {
        private static CenterBanner? _banner;

        /// <summary>Called once from a page's code-behind after the control is loaded.</summary>
        public static void Register(CenterBanner banner) => _banner = banner;

        /// <summary>Shows the banner. Safe to call from any thread.</summary>
        public static void Show(string title, string? sub = null,
                                BannerKind kind = BannerKind.Success, int durationMs = 2200)
        {
            if (_banner == null) return;
            _banner.Dispatcher.Invoke(() => _banner.Show(title, sub, kind, durationMs));
        }

        public static void Success(string title, string? sub = null)
            => Show(title, sub, BannerKind.Success, 2200);

        public static void Error(string title, string? sub = null)
            => Show(title, sub, BannerKind.Error, 3000);

        public static void Warning(string title, string? sub = null)
            => Show(title, sub, BannerKind.Warning, 2800);
    }
}
