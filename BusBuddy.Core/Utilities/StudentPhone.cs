namespace BusBuddy.Core.Utilities;

/// <summary>
/// Canonical US phone format for student contact fields. Empty is allowed. A value that
/// cannot be reduced to 10 digits (optional leading country code 1) is rejected.
/// </summary>
public static class StudentPhone
{
    /// <summary>
    /// True when <paramref name="phone"/> is empty or a 10-digit number (11 digits with a leading 1).
    /// <paramref name="normalized"/> is null for empty input, or <c>(NPA) NXX-XXXX</c> when valid.
    /// </summary>
    public static bool TryNormalize(string? phone, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            normalized = null;
            return true;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits[0] == '1')
        {
            digits = digits[1..];
        }

        if (digits.Length != 10)
        {
            normalized = null;
            return false;
        }

        normalized = $"({digits[..3]}) {digits.Substring(3, 3)}-{digits.Substring(6, 4)}";
        return true;
    }
}
