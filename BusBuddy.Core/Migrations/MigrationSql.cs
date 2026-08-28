using Microsoft.EntityFrameworkCore.Migrations;

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Provider-specific SQL fragments so one migration chain can apply on SQL Server and Postgres.
/// </summary>
internal static class MigrationSql
{
    public static bool IsNpgsql(MigrationBuilder builder) =>
        string.Equals(
            builder.ActiveProvider,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            StringComparison.Ordinal);

    public static string BoolType(MigrationBuilder builder) =>
        IsNpgsql(builder) ? "boolean" : "bit";

    public static string DateTimeType(MigrationBuilder builder) =>
        IsNpgsql(builder) ? "timestamp with time zone" : "datetime2";

    public static string StringType(MigrationBuilder builder, int? maxLength = null)
    {
        if (IsNpgsql(builder))
        {
            return maxLength is null or < 1 ? "text" : $"character varying({maxLength})";
        }

        return maxLength is null or < 1 ? "nvarchar(max)" : $"nvarchar({maxLength})";
    }

    public static string UtcNow(MigrationBuilder builder) =>
        IsNpgsql(builder) ? "CURRENT_TIMESTAMP" : "GETUTCDATE()";

    public static string NotNullFilter(MigrationBuilder builder, string column) =>
        IsNpgsql(builder) ? $"\"{column}\" IS NOT NULL" : $"[{column}] IS NOT NULL";
}
