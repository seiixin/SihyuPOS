#nullable enable
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace SihyuPOSPayroll.Helpers
{
    /// <summary>
    /// Converts "empty" values to Visibility.Collapsed (or Hidden).
    /// - string: null/empty/whitespace => Collapsed
    /// - ICollection: Count == 0 => Collapsed
    /// - IEnumerable: no items => Collapsed
    /// - int: 0 => Collapsed
    /// - null => Collapsed
    /// Use parameter "Invert" to flip behavior.
    /// Use parameter "Hidden" to return Visibility.Hidden instead of Collapsed.
    /// Examples:
    ///   Visibility="{Binding Status, Converter={StaticResource EmptyStringToVisibilityConverter}}"
    ///   Visibility="{Binding Status, Converter={StaticResource EmptyStringToVisibilityConverter}, ConverterParameter=Invert}"
    ///   Visibility="{Binding Items, Converter={StaticResource EmptyStringToVisibilityConverter}, ConverterParameter=Hidden}"
    /// </summary>
    public sealed class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var param = parameter?.ToString() ?? string.Empty;
            var invert = param.IndexOf("Invert", StringComparison.OrdinalIgnoreCase) >= 0;
            var useHidden = param.IndexOf("Hidden", StringComparison.OrdinalIgnoreCase) >= 0;

            bool isEmpty = IsEmpty(value);

            // If invert, Visible when empty; otherwise Visible when not empty
            bool visible = invert ? isEmpty : !isEmpty;

            if (visible) return Visibility.Visible;
            return useHidden ? Visibility.Hidden : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static bool IsEmpty(object? value)
        {
            switch (value)
            {
                case null:
                    return true;

                case string s:
                    return string.IsNullOrWhiteSpace(s);

                case ICollection col:
                    return col.Count == 0;

                case IEnumerable en:
                    // Avoid forcing full enumeration if possible
                    var enumerator = en.GetEnumerator();
                    try { return !enumerator.MoveNext(); }
                    finally { (enumerator as IDisposable)?.Dispose(); }

                case int n:
                    return n == 0;

                default:
                    return false; // treat unknown types as "not empty"
            }
        }
    }
}
