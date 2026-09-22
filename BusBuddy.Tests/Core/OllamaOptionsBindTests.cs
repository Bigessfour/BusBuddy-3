using BusBuddy.Core.Configuration;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class OllamaOptionsBindTests
{
    [Test]
    public void Bind_ClampsOutOfRangeNumbers_AndKeepsDefaultsWhenMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:TimeoutSeconds"] = "0",
                ["Ollama:MaxTokens"] = "999999",
                ["Ollama:Temperature"] = "9.5",
                ["Ollama:Model"] = "mistral",
            })
            .Build();

        var options = OllamaOptions.Bind(configuration);

        Assert.That(options.TimeoutSeconds, Is.EqualTo(1));
        Assert.That(options.MaxTokens, Is.EqualTo(256_000));
        Assert.That(options.Temperature, Is.EqualTo(2.0));
        Assert.That(options.Model, Is.EqualTo("mistral"));
        Assert.That(options.Enabled, Is.True);
        Assert.That(options.BaseUrl, Is.EqualTo("http://localhost:11434/v1"));
    }

    [Test]
    public void Bind_AcceptsLegacyUseLiveApiKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:UseLiveAPI"] = "false",
            })
            .Build();

        var options = OllamaOptions.Bind(configuration);

        Assert.That(options.Enabled, Is.False);
    }
}
