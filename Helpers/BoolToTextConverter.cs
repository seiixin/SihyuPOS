#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;

namespace SihyuPOSPayroll.Helpers
{
    [ValueConversion(typeof(object), typeof(string))]
    public sealed class BoolToTextConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string active = "Active";
            string inactive = "Inactive";
            string unknown = "No Login";

            if (parameter is string p && !string.IsNullOrWhiteSpace(p))
            {
                var parts = p.Split('|');
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])) active = parts[0];
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) inactive = parts[1];
                if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2])) unknown = parts[2];
            }

            bool? b = ToNullableBool(value);
            return b == true ? active : b == false ? inactive : unknown;
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
                // Fully qualify to avoid clashing with IValueConverter.Convert method
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
