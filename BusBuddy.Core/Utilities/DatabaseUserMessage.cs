using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Turns low-level Npgsql/EF failures into operator-friendly text for WPF dialogs.
/// </summary>
public static class DatabaseUserMessage
{
    public const string UnavailableShort =
        "Database is unavailable. Ensure Postgres is running and this machine can reach the Mac host on port 5432.";

    public static string UnavailableForOperation(string operationDescription)
    {
        var endpoint = DescribeConfiguredEndpoint();
        return string.IsNullOrWhiteSpace(endpoint)
            ? $"Cannot {operationDescription}. {UnavailableShort}"
            : $"Cannot {operationDescription}: unable to reach Postgres at {endpoint}. " +
              "On the Mac host, confirm Docker Postgres is running (docker compose up). " +
              "On this VM, set BUSBUDDY_CONNECTION to the Mac LAN IP from ipconfig getifaddr en0 " +
              "and verify port 5432 is allowed through the firewall.";
    }

    public static async Task<bool> CanConnectAsync(
        DbContext? context,
        int timeoutSeconds = 5,
        CancellationToken cancellationToken = default)
    {
        if (context is null)
        {
            return false;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            return await context.Database.CanConnectAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (IsConnectivityFailure(ex))
        {
            return false;
        }
    }

    public static string ForOperation(Exception exception, string operationDescription)
    {
        if (IsConnectivityFailure(exception))
        {
            var endpoint = DescribeConfiguredEndpoint();
            return string.IsNullOrWhiteSpace(endpoint)
                ? $"Cannot {operationDescription}. {UnavailableShort}"
                : $"Cannot {operationDescription}: unable to reach Postgres at {endpoint}. " +
                  "On the Mac host, confirm Docker Postgres is running (docker compose up). " +
                  "On this VM, set BUSBUDDY_CONNECTION to the Mac LAN IP from ipconfig getifaddr en0 " +
                  "and verify port 5432 is allowed through the firewall.";
        }

        return $"Failed to {operationDescription}: {exception.Message}";
    }

    public static bool IsConnectivityFailure(Exception? exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is TimeoutException)
            {
                return true;
            }

            if (ex is NpgsqlException npgsql)
            {
                if (npgsql.InnerException is TimeoutException)
                {
                    return true;
                }

                if (npgsql.Message.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase)
                    || npgsql.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (ex is InvalidOperationException invalid
                && invalid.Message.Contains("transient failure", StringComparison.OrdinalIgnoreCase)
                && HasConnectivityInner(invalid))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasConnectivityInner(Exception exception)
    {
        for (var ex = exception.InnerException; ex is not null; ex = ex.InnerException)
        {
            if (ex is NpgsqlException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    private static string? DescribeConfiguredEndpoint()
    {
        var raw = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
        return PostgresConnectionResolver.DescribeEndpoint(raw);
    }
}
