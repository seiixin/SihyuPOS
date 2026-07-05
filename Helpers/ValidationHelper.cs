#nullable enable
using System;
using System.Text.RegularExpressions;

namespace SihyuPOSPayroll.Helpers
{
    public static class ValidationHelper
    {
        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return regex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsValidPhoneNumber(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            try
            {
                var regex = new Regex(@"^[\d\s\+\-\+\(\)]{7,}$");
                return regex.IsMatch(phoneNumber);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsValidPositiveNumber(decimal? number)
        {
            return number.HasValue && number.Value > 0;
        }

        public static bool IsValidPositiveInteger(int? number)
        {
            return number.HasValue && number.Value > 0;
        }

        public static bool IsValidRequiredString(string? value, int minLength = 1, int maxLength = 255)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length >= minLength && value.Length <= maxLength;
        }

        public static bool IsValidDate(DateTime? date)
        {
            return date.HasValue && date.Value <= DateTime.Now;
        }

        public static bool IsValidFutureDate(DateTime? date)
        {
            return date.HasValue && date.Value >= DateTime.Now.Date;
        }

        public static bool IsValidPassword(string? password)
        {
            return !string.IsNullOrWhiteSpace(password) && password.Length >= 6;
        }
    }
}
