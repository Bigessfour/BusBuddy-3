namespace BusBuddy.WPF.Utilities;

/// <summary>Formats clerk-entered phone numbers before persistence.</summary>
public static class StudentPhoneNormalizer
{
    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return phone;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length == 10
            ? $"({digits[..3]}) {digits.Substring(3, 3)}-{digits.Substring(6, 4)}"
            : phone;
    }
}
