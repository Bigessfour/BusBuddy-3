using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BusBuddy.Core.Data.Configurations;

/// <summary>
/// Store-wide DateTime Kind labeling for <c>timestamp with time zone</c>.
/// Labels as UTC without shifting — wall-clock district times stay on the same calendar day.
/// </summary>
internal static class UtcDateTimeConvention
{
    private static readonly ValueConverter<DateTime, DateTime> DateTimeUtc = new(
        v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableDateTimeUtc = new(
        v => !v.HasValue || v.Value == DateTime.MinValue
            ? null
            : (v.Value.Kind == DateTimeKind.Utc
                ? v.Value
                : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)),
        v => v.HasValue
            ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
            : DateTime.MinValue);

    internal static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(string) && !property.IsNullable)
                {
                    property.SetDefaultValue("");
                }
                else if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(DateTimeUtc);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(NullableDateTimeUtc);
                }
            }
        }
    }
}
