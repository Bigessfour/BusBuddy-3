using BusBuddy.Core.Utilities;

namespace BusBuddy.WPF.Utilities;

/// <summary>Formats clerk-entered phone numbers before persistence. Invalid values are left unchanged.</summary>
public static class StudentPhoneNormalizer
{
    public static string? Normalize(string? phone) =>
        StudentPhone.TryNormalize(phone, out var normalized) ? normalized : phone;
}
