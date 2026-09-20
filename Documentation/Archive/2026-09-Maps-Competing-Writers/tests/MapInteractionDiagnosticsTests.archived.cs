// Archived 2026-09. Not compiled. Tests existed only to lock MapInteractionDiagnostics
// into the WPF product. Contract proof is MapViewTests + MapsConnectionProbe + SfMapTileProbe.
// Original location: BusBuddy.Tests/WPF/MapViewModelTests.cs

#if false
    [Test]
    public void MapInteractionDiagnostics_IsGatedByConfigWithEnvOverride()
    {
        var previous = Environment.GetEnvironmentVariable(MapInteractionDiagnostics.EnvironmentOverride);
        try
        {
            Environment.SetEnvironmentVariable(MapInteractionDiagnostics.EnvironmentOverride, null);
            var on = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [MapInteractionDiagnostics.ConfigKey] = "true" })
                .Build();
            var off = new ConfigurationBuilder().Build();

            Assert.That(MapInteractionDiagnostics.IsEnabled(on), Is.True);
            Assert.That(MapInteractionDiagnostics.IsEnabled(off), Is.False);
            Assert.That(MapInteractionDiagnostics.IsEnabled(null), Is.False);

            Environment.SetEnvironmentVariable(MapInteractionDiagnostics.EnvironmentOverride, "0");
            Assert.That(MapInteractionDiagnostics.IsEnabled(on), Is.False, "env var wins over config");
            Environment.SetEnvironmentVariable(MapInteractionDiagnostics.EnvironmentOverride, "1");
            Assert.That(MapInteractionDiagnostics.IsEnabled(off), Is.True);
        }
        finally
        {
            Environment.SetEnvironmentVariable(MapInteractionDiagnostics.EnvironmentOverride, previous);
        }
    }

    [Test]
    public void MapInteractionDiagnostics_BreadcrumbsNameKeysNotCharacters()
    {
        Assert.That(
            MapInteractionDiagnostics.DescribeKey(System.Windows.Input.Key.A, System.Windows.Input.ModifierKeys.Control),
            Is.EqualTo("Control+A"));
        Assert.That(
            MapInteractionDiagnostics.DescribeKey(System.Windows.Input.Key.OemPlus, System.Windows.Input.ModifierKeys.None),
            Is.EqualTo("OemPlus"));

        var line = MapInteractionDiagnostics.FormatBreadcrumb(1234, "wheel", "delta=120");
        Assert.That(line, Does.StartWith("+   1234ms"));
        Assert.That(line, Does.Contain("wheel"));
        Assert.That(line, Does.EndWith("delta=120"));

        var layer = XamlViewFile.Read("Utilities/GoogleMapTilesImageryLayer.cs");
        Assert.That(layer, Does.Contain("TileRequestedEventArgs(Scale, X, Y"));
        var diag = XamlViewFile.Read("Utilities/MapInteractionDiagnostics.cs");
        Assert.That(diag, Does.Not.Contain("ResolveTileUrl"));
        Assert.That(diag, Does.Not.Contain("UrlTemplate"));
        Assert.That(diag, Does.Contain("map-interactions-.log"));
        Assert.That(diag, Does.Contain("PresentationTraceSources.DataBindingSource"));
        Assert.That(diag, Does.Contain("UnhandledException += OnDispatcherUnhandledException"));
    }
#endif
