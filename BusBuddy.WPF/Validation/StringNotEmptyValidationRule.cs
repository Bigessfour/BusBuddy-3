using System;
using System.Globalization;
using System.Windows.Controls;

namespace BusBuddy.WPF.Validation
{
    /// <summary>
    /// WPF ValidationRule that ensures a string (or string-like value) is not null, empty, or whitespace.
    /// Follows official WPF validation patterns — see: https://learn.microsoft.com/dotnet/desktop/wpf/data/data-validation
    /// </summary>
    public class StringNotEmptyValidationRule : ValidationRule
    {
        /// <summary>
        /// Optional field label used in the error message.
        /// </summary>
        public string FieldName { get; set; } = "Field";

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            // Handle common binding value cases (string, nullables, boxed values)
            if (value is null)
            {
                return new ValidationResult(false, $"{FieldName} is required.");
            }

            // Some controls (e.g., masked editors) may box the value as object — normalize to string when possible
            string text = value switch
            {
                string s => s,
                _ => value.ToString() ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(text))
            {
                return new ValidationResult(false, $"{FieldName} is required.");
            }

            return ValidationResult.ValidResult;
        }
    }
}
