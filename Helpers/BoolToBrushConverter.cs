#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SihyuPOSPayroll.Helpers
{
    [ValueConversion(typeof(object), typeof(Brush))]
    public sealed class BoolToBrushConverter : IValueConverter
    {
        private static readonly Brush Active = (Brush)new BrushConverter().ConvertFromString("#16A34A")!; // green
        private static readonly Brush Inactive = (Brush)new BrushConverter().ConvertFromString("#DC2626")!; // red
        private static readonly Brush Unknown = (Brush)new BrushConverter().ConvertFromString("#9CA3AF")!; // gray

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool? b = ToNullableBool(value);
            return b == true ? Active : b == false ? Inactive : Unknown;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static bool? ToNullableBool(object? value)
        {
            if (value is null) return null;
            if (value is bool b) return b;

            if (value is string s)
            {
                s = s.Trim().ToLowerInvariant();
                if (s is "true" or "1" or "yes" or "y" or "active") return true;
                if (s is "false" or "0" or "no" or "n" or "inactive") return false;
                return null;
            }

            try
            {
                // Fully qualify to avoid clashing with IValueConverter.Convert method name
                var i = global::System.Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return i != 0;
            }
            catch
            {
                return null;
            }
        }
    }
}
