using System.Text.RegularExpressions;
using Serilog;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// Resolves Postgres connection strings from environment variables (highest precedence per
/// https://learn.microsoft.com/ef/core/miscellaneous/connection-strings and
/// https://learn.microsoft.com/aspnet/core/fundamentals/configuration).
/// Refreshes stale Mac host IPs written by <c>run-wpf.sh</c> into <c>keys/mac-host-ip.txt</c>.
/// </summary>
public static class PostgresConnectionResolver
{
    private static readonly ILogger Logger = Log.ForContext(typeof(PostgresConnectionResolver));

    private static readonly Regex HostRegex = new(
        @"Host=([^;]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string DefaultDatabase = "busbuddy_test";
    private const string DefaultUser = "busbuddy";
    private const string DefaultPassword = "busbuddy_dev";
    internal const int ConnectTimeoutSeconds = 5;

    /// <summary>
    /// Reads <c>BUSBUDDY_CONNECTION</c>, optionally refreshes the host from <c>keys/mac-host-ip.txt</c>,
    /// and writes the resolved value back to the process environment.
    /// </summary>
    public static string? ResolveAndApply()
    {
        var macHostIp = TryReadMacHostIp();
        var current = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");

        if (string.IsNullOrWhiteSpace(current))
        {
            if (string.IsNullOrWhiteSpace(macHostIp))
            {
                return null;
            }

            var built = EnsureConnectTimeout(BuildConnectionString(macHostIp));
            Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", built);
            Logger.Information("Set BUSBUDDY_CONNECTION from mac-host-ip.txt -> Host={Host}", macHostIp);
            return built;
        }

        if (!IsPostgresConnection(current))
        {
            return current;
        }

        var resolved = current;
        if (!string.IsNullOrWhiteSpace(macHostIp))
        {
            resolved = RefreshHostIfNeeded(current, macHostIp);
            if (!string.Equals(resolved, current, StringComparison.Ordinal))
            {
                Logger.Warning(
                    "Refreshed stale Postgres host in BUSBUDDY_CONNECTION: {OldHost} -> {NewHost}",
                    ExtractHost(current),
                    macHostIp);
            }
        }

        resolved = EnsureConnectTimeout(resolved);
        if (!string.Equals(resolved, current, StringComparison.Ordinal))
        {
            Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", resolved);
        }

        return resolved;
    }

    /// <summary>
    /// Replaces the Postgres host when it differs from the Mac LAN IP supplied by the launcher.
    /// </summary>
    public static string RefreshHostIfNeeded(string connectionString, string macHostIp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(macHostIp);

        if (!IsPostgresConnection(connectionString))
        {
            return connectionString;
        }

        var currentHost = ExtractHost(connectionString);
        return !string.IsNullOrWhiteSpace(currentHost)
               && !string.Equals(currentHost, macHostIp, StringComparison.OrdinalIgnoreCase)
            ? ReplaceHost(connectionString, macHostIp)
            : connectionString;
    }

    public static bool IsPostgresConnection(string? connectionString) =>
        !string.IsNullOrWhiteSpace(connectionString)
        && (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("postgres", StringComparison.OrdinalIgnoreCase));

    public static string? DescribeEndpoint(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var host = ExtractHost(connectionString);
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var portMatch = Regex.Match(connectionString, @"Port=(\d+)", RegexOptions.IgnoreCase);
        var port = portMatch.Success ? portMatch.Groups[1].Value : "5432";
        return $"{host}:{port}";
    }

    public static string BuildConnectionString(string host) =>
        $"Host={host};Port=5432;Database={DefaultDatabase};Username={DefaultUser};Password={DefaultPassword};Include Error Detail=true;Timeout={ConnectTimeoutSeconds}";

    /// <summary>
    /// Caps Npgsql connect wait so an unreachable Mac host fails in seconds, not the 15s default.
    /// </summary>
    public static string EnsureConnectTimeout(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (!IsPostgresConnection(connectionString))
        {
            return connectionString;
        }

        if (Regex.IsMatch(connectionString, @"(^|;)\s*Timeout\s*=", RegexOptions.IgnoreCase))
        {
            return connectionString;
        }

        return connectionString.TrimEnd(';') + $";Timeout={ConnectTimeoutSeconds}";
    }

    internal static string? ExtractHost(string connectionString)
    {
        var match = HostRegex.Match(connectionString);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    internal static string ReplaceHost(string connectionString, string newHost) =>
        HostRegex.Replace(connectionString, $"Host={newHost}", 1);

    private static string? TryReadMacHostIp()
    {
        foreach (var path in EnumerateMacHostIpPaths())
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var ip = File.ReadAllText(path).Trim();
                if (Regex.IsMatch(ip, @"^\d{1,3}(\.\d{1,3}){3}$"))
                {
                    Logger.Debug("Using Mac host IP from {Path}: {Host}", path, ip);
                    return ip;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Unable to read Mac host IP file at {Path}", path);
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateMacHostIpPaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in EnumerateMacHostIpPathsCore())
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var full = Path.GetFullPath(path);
            if (seen.Add(full))
            {
                yield return full;
            }
        }
    }

    private static IEnumerable<string> EnumerateMacHostIpPathsCore()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        yield return Path.Combine(baseDir, "keys", "mac-host-ip.txt");

        var dir = new DirectoryInfo(baseDir);
        for (var depth = 0; depth < 6 && dir is not null; depth++)
        {
            yield return Path.Combine(dir.FullName, "keys", "mac-host-ip.txt");
            dir = dir.Parent;
        }

        yield return @"C:\dev\BusBuddy-3\keys\mac-host-ip.txt";
        yield return @"Z:\keys\mac-host-ip.txt";
        yield return @"Z:\BusBuddy-3\keys\mac-host-ip.txt";
    }
}
