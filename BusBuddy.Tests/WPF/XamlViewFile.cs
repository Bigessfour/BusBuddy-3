using System.IO;
using System.Linq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

internal static class XamlViewFile
{
    public static bool Exists(string relativeUnderWpf) => Resolve(relativeUnderWpf) is not null;

    public static string Read(string relativeUnderWpf)
    {
        var path = Resolve(relativeUnderWpf);
        if (path is null)
        {
            throw new FileNotFoundException($"Missing BusBuddy.WPF/{relativeUnderWpf}");
        }

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Concatenates every <c>*.cs</c> under a folder. Use this instead of <see cref="Read"/> when the
    /// assertion is that some wiring exists in a layer, not that it lives in one particular file —
    /// otherwise splitting a class into collaborators fails the test without changing behaviour.
    /// </summary>
    public static string ReadFolder(string relativeFolderUnderWpf)
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "BusBuddy.WPF", relativeFolderUnderWpf);
            if (Directory.Exists(candidate))
            {
                return string.Join(
                    "\n",
                    Directory.EnumerateFiles(candidate, "*.cs", SearchOption.AllDirectories)
                        .OrderBy(p => p, System.StringComparer.Ordinal)
                        .Select(File.ReadAllText));
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException($"Missing BusBuddy.WPF/{relativeFolderUnderWpf}");
    }

    private static string? Resolve(string relativeUnderWpf)
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "BusBuddy.WPF", relativeUnderWpf);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }
}

/// <summary>
/// Reads source under BusBuddy.Core so MapViewModel cannot ship without
/// IMapsGeoService, DistrictDepot, and RouteDrivePathRefresher.
/// </summary>
internal static class CoreSourceFile
{
    public static bool Exists(string relativeUnderCore) => Resolve(relativeUnderCore) is not null;

    public static string Read(string relativeUnderCore)
    {
        var path = Resolve(relativeUnderCore);
        if (path is null)
        {
            throw new FileNotFoundException($"Missing BusBuddy.Core/{relativeUnderCore}");
        }

        return File.ReadAllText(path);
    }

    private static string? Resolve(string relativeUnderCore)
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "BusBuddy.Core", relativeUnderCore);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }
}
