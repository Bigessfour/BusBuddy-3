namespace BusBuddy.Core.Utilities;

/// <summary>
/// Loads simple KEY=value lines from a .env file into the process environment.
/// </summary>
public static class EnvFileLoader
{
    public static int LoadIntoEnvironment(IEnumerable<string> paths, bool overwrite = true)
    {
        var loaded = 0;
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim().Trim('"').Trim('\'');
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!overwrite && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    continue;
                }

                Environment.SetEnvironmentVariable(key, value);
                loaded++;
            }
        }

        return loaded;
    }

    public static IEnumerable<string> GetKeysEnvFileCandidates()
    {
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "keys", ".env"));
        yield return Path.Combine(Directory.GetCurrentDirectory(), "keys", ".env");
        yield return @"C:\dev\BusBuddy-3\keys\.env";
        yield return @"C:\dev\busbuddy\keys\.env";
        yield return @"C:\dev\BusBuddy\keys\.env";
    }
}
