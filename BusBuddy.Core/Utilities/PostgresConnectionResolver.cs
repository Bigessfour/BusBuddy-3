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
                ApplyDatabaseProvider(null);
                return null;
            }

            var built = EnsureConnectTimeout(BuildConnectionString(macHostIp));
            Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", built);
            Logger.Information("Set BUSBUDDY_CONNECTION from mac-host-ip.txt -> Host={Host}", macHostIp);
            ApplyDatabaseProvider(built);
            return built;
        }

        if (!IsPostgresConnection(current))
        {
            ApplyDatabaseProvider(current);
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

        ApplyDatabaseProvider(resolved);
        return resolved;
    }

    /// <summary>
    /// Postgres unless the clerk explicitly asked for SQL Server / LocalDB / SQLite.
    /// A live <c>Host=</c> Npgsql string always wins.
    /// </summary>
    public static string ResolveProvider(string? declaredProvider, string? connectionString)
    {
        if (IsPostgresConnection(connectionString))
        {
            return "Postgres";
        }

        if (IsExplicitSqlite(declaredProvider))
        {
            return "Local";
        }

        if (IsExplicitSqlServer(declaredProvider))
        {
            return declaredProvider!.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                ? "SqlServer"
                : "LocalDB";
        }

        return "Postgres";
    }

    /// <summary>
    /// Writes the resolved provider into the process environment so
    /// <c>AddEnvironmentVariables()</c> matches the live connection.
    /// </summary>
    public static string ApplyDatabaseProvider(string? connectionString)
    {
        var declared = Environment.GetEnvironmentVariable("DatabaseProvider");
        var effective = ResolveProvider(declared, connectionString);
        if (!string.Equals(declared, effective, StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable("DatabaseProvider", effective);
            Logger.Information("Set DatabaseProvider={Provider} from live connection", effective);
        }

        return effective;
    }

    private static bool IsExplicitSqlite(string? provider) =>
        string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase);

    private static bool IsExplicitSqlServer(string? provider) =>
        string.Equals(provider, "LocalDB", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Replaces the Postgres host when it differs from the Mac LAN IP supplied by the launcher.
    /// Loopback hosts (<c>localhost</c> / <c>127.0.0.1</c>) are left alone so Mac-side DbPrep
    /// and local Docker stay on the published port instead of the UTM shared-network IP.
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
        if (string.IsNullOrWhiteSpace(currentHost)
            || IsLoopbackHost(currentHost)
            || string.Equals(currentHost, macHostIp, StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        return ReplaceHost(connectionString, macHostIp);
    }

    internal static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);

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
