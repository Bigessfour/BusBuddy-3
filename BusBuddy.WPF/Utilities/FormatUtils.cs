using System;
using System.Globalization;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Provides centralized formatting utilities for consistent UI presentation
    /// </summary>
    public static class FormatUtils
    {
        // Date formatting constants
        public const string ShortDateFormat = "MM/dd/yyyy";
        public const string LongDateFormat = "MMMM dd, yyyy";
        public const string TimeFormat = "h:mm tt";
        public const string DateTimeFormat = "MM/dd/yyyy h:mm tt";

        // Currency formatting
        public const string CurrencyFormat = "C2";

        // Number formatting
        public const string MileageFormat = "N1"; // 1 decimal place
        public const string PercentFormat = "P1"; // 1 decimal place

        /// <summary>
        /// Formats a date using the standard short date format (MM/dd/yyyy)
        /// </summary>
        /// <param name="date">The date to format</param>
        /// <returns>Formatted date string or empty if null</returns>
        public static string FormatDate(DateTime? date)
        {
            return date.HasValue
                ? date.Value.ToString(ShortDateFormat, CultureInfo.CurrentCulture)
                : string.Empty;
        }

        /// <summary>
        /// Formats a date using the full date format (Month dd, yyyy)
        /// </summary>
        /// <param name="date">The date to format</param>
        /// <returns>Formatted date string or empty if null</returns>
        public static string FormatLongDate(DateTime? date)
        {
            return date.HasValue
                ? date.Value.ToString(LongDateFormat, CultureInfo.CurrentCulture)
                : string.Empty;
        }

        /// <summary>
        /// Formats a time value (h:mm tt)
        /// </summary>
        /// <param name="time">The TimeSpan to format</param>
        /// <returns>Formatted time string or empty if null</returns>
        public static string FormatTime(TimeSpan? time)
        {
            if (!time.HasValue)
            {
                return string.Empty;
            }


            DateTime dateTime = DateTime.Today.Add(time.Value);
            return dateTime.ToString(TimeFormat, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Formats a status with consistent styling (first letter capitalized)
        /// </summary>
        /// <param name="status">The status string</param>
        /// <returns>Formatted status or "Unknown" if null or empty</returns>
        public static string FormatStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {

                return "Unknown";
            }

            // Capitalize first letter

            if (status.Length == 1)
            {

                return status.ToUpper();
            }


            return char.ToUpper(status[0]) + status.Substring(1).ToLower();
        }

        /// <summary>
        /// Formats a decimal value as currency
        /// </summary>
        /// <param name="value">The decimal value</param>
        /// <returns>Formatted currency string or empty if null</returns>
        public static string FormatCurrency(decimal? value)
        {
            return value.HasValue
                ? value.Value.ToString(CurrencyFormat, CultureInfo.CurrentCulture)
                : string.Empty;
        }

        /// <summary>
        /// Formats a decimal value with a fixed number of decimal places
        /// </summary>
        /// <param name="value">The decimal value</param>
        /// <param name="decimalPlaces">Number of decimal places (default 1)</param>
        /// <returns>Formatted numeric string or empty if null</returns>
        public static string FormatDecimal(decimal? value, int decimalPlaces = 1)
        {
            if (!value.HasValue)
            {
                return string.Empty;
            }


            string format = "N" + decimalPlaces;
            return value.Value.ToString(format, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Formats a value as a percentage
        /// </summary>
        /// <param name="value">The decimal value (where 1.0 = 100%)</param>
        /// <returns>Formatted percentage string or empty if null</returns>
        public static string FormatPercent(decimal? value)
        {
            return value.HasValue
                ? value.Value.ToString(PercentFormat, CultureInfo.CurrentCulture)
                : string.Empty;
        }

        /// <summary>
        /// Returns a standard display for mileage values
        /// </summary>
        /// <param name="miles">The mileage value</param>
        /// <returns>Formatted mileage with "mi" suffix or empty if null</returns>
        public static string FormatMileage(decimal? miles)
        {
            if (!miles.HasValue)
            {
                return string.Empty;
            }


            return $"{miles.Value.ToString(MileageFormat, CultureInfo.CurrentCulture)} mi";
        }

        /// <summary>
        /// Returns a standard display for duration in minutes
        /// </summary>
        /// <param name="minutes">Duration in minutes</param>
        /// <returns>Formatted duration as hours and minutes</returns>
        public static string FormatDuration(int? minutes)
        {
            if (!minutes.HasValue || minutes.Value <= 0)
            {
                return string.Empty;
            }


            int hours = minutes.Value / 60;
            int mins = minutes.Value % 60;

            if (hours > 0)
            {

                return $"{hours}h {mins}m";
            }
            else
            {

                return $"{mins}m";
            }

        }

        /// <summary>
        /// Formats a phone number in standard format (XXX) XXX-XXXX
        /// </summary>
        /// <param name="phoneNumber">The raw phone number</param>
        /// <returns>Formatted phone number or the original if invalid</returns>
        public static string FormatPhoneNumber(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {

                return string.Empty;
            }

            // Remove any non-digit characters

            string digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());

            // Format as (XXX) XXX-XXXX if 10 digits
            if (digitsOnly.Length == 10)
            {
                return $"({digitsOnly.Substring(0, 3)}) {digitsOnly.Substring(3, 3)}-{digitsOnly.Substring(6)}";
            }
            // Format as XXX-XXXX if 7 digits
            else if (digitsOnly.Length == 7)
            {
                return $"{digitsOnly.Substring(0, 3)}-{digitsOnly.Substring(3)}";
            }

            // Return original if not a standard format
            return phoneNumber;
        }
    }
}
