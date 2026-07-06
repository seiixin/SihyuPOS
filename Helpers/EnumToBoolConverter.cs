#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;

namespace SihyuPOSPayroll.Helpers
{
    /// <summary>
    /// Converts an Enum value to a Boolean for RadioButton IsChecked binding.
    /// The parameter specifies which enum value should return true.
    /// </summary>
    [ValueConversion(typeof(Enum), typeof(bool))]
    public sealed class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            // Check if the enum value matches the parameter
            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not bool isChecked || !isChecked || parameter == null)
                return Binding.DoNothing;

            // Return the enum value that matches the parameter
            return Enum.Parse(targetType, parameter.ToString()!);
        }
    }
}
