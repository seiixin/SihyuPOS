using System;
using SihyuPOSPayroll.Views.Components;

namespace SihyuPOSPayroll.Services
{
    /// <summary>
    /// Lightweight message-bus that connects ViewModels (which fire Show requests)
    /// to the <see cref="ToastNotification"/> control that lives in the View.
    ///
    /// Usage:
    ///   View registers:  ToastService.Register(myToastControl);
    ///   ViewModel calls: ToastService.Show("Saved!", ToastKind.Success);
    /// </summary>
    public static class ToastService
    {
        private static ToastNotification? _toast;

        /// <summary>Called once from code-behind after the toast control is created.</summary>
        public static void Register(ToastNotification toast)
            => _toast = toast;

        /// <summary>Shows a toast; safe to call from any UI thread.</summary>
        public static void Show(string message, ToastKind kind = ToastKind.Success, int durationMs = 2800)
        {
            if (_toast == null) return;

            _toast.Dispatcher.Invoke(() => _toast.Show(message, kind, durationMs));
        }

        public static void Success(string message) => Show(message, ToastKind.Success);
        public static void Error(string message)   => Show(message, ToastKind.Error,   3500);
        public static void Warning(string message) => Show(message, ToastKind.Warning, 3200);
        public static void Info(string message)    => Show(message, ToastKind.Info);
    }
}
