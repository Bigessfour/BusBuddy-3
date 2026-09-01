using System.IO;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

internal static class XamlViewFile
{
    public static string Read(string relativeUnderWpf)
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "BusBuddy.WPF", relativeUnderWpf);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException($"Missing BusBuddy.WPF/{relativeUnderWpf}");
    }
}
