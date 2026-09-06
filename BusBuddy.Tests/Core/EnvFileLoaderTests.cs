using System.IO;
using BusBuddy.Core.Utilities;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class EnvFileLoaderTests
{
    [Test]
    public void LoadIntoEnvironment_ParsesKeyValuePairs()
    {
        var path = Path.Combine(Path.GetTempPath(), $"busbuddy-env-{Guid.NewGuid()}.env");
        try
        {
            File.WriteAllText(path, """
                # comment
                SYNCFUSION_LICENSE_KEY=Ngo9BigBOggjTestKeyValueHerePadding==
                GOOGLE_MAPS_API_KEY=AIza-test
                """);

            var loaded = EnvFileLoader.LoadIntoEnvironment(new[] { path });
            Assert.That(loaded, Is.EqualTo(2));
            Assert.That(Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY"), Does.StartWith("Ngo9"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", null);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
