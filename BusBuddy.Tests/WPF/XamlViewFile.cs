using System.IO;
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
