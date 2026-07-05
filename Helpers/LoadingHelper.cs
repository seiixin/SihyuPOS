#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SihyuPOSPayroll.Helpers
{
    public static class LoadingHelper
    {
        private static Window? _loadingWindow;

        public static void ShowLoading(string message = "Loading...")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_loadingWindow != null)
                    return;

                _loadingWindow = new Window
                {
                    Width = 300,
                    Height = 120,
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Background = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    BorderThickness = new Thickness(1),
                    ShowInTaskbar = false
                };

                var grid = new Grid();
                var textBlock = new TextBlock
                {
                    Text = message,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 16,
                    Margin = new Thickness(20)
                };
                grid.Children.Add(textBlock);
                _loadingWindow.Content = grid;

                if (Application.Current.MainWindow != null)
                {
                    _loadingWindow.Owner = Application.Current.MainWindow;
                }

                _loadingWindow.Show();
            });
        }

        public static void HideLoading()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_loadingWindow != null)
                {
                    _loadingWindow.Close();
                    _loadingWindow = null;
                }
            });
        }
    }
}
