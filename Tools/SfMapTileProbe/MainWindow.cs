using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Maps;

namespace SfMapTileProbe;

/// <summary>
/// Isolates Syncfusion ImageryLayer tile paint vs network.
/// Modes (first CLI arg): osm | osm-https | google-urltemplate | google-geturi
/// </summary>
public partial class App : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0] : "osm";
        var app = new App();
        app.Run(new MainWindow(mode));
    }
}

public sealed class MainWindow : Window
{
    private readonly string _mode;
    private readonly string _logPath;
    private readonly ImageryLayer _layer;
    private readonly SfMap _map;
    private readonly TextBlock _status;

    public MainWindow(string mode)
    {
        _mode = mode;
        Title = "SfMap tile probe — " + mode;
        Width = 960;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var logs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "BusBuddy.WPF", "bin", "Debug", "logs"));
        Directory.CreateDirectory(logs);
        _logPath = Path.Combine(logs, $"sfmap-probe-{mode}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

        // Match BusBuddy FluentDark application (possible style interaction).
        SfSkinManager.ApplyThemeAsDefaultStyle = true;
        SfSkinManager.SetTheme(this, new Theme("FluentDark"));

        _map = new SfMap
        {
            EnableZoom = true,
            EnablePan = true,
            MinZoom = 1,
            MaxZoom = 19,
            ZoomLevel = 4,
        };

        _layer = mode is "google-geturi"
            ? new GoogleGetUriLayer()
            : new ImageryLayer();

        _layer.Center = new Point(38.0872, -102.6208);

        switch (mode)
        {
            case "osm":
                _layer.LayerType = LayerType.OSM;
                break;
            case "osm-https":
                // Documented custom tiles path (Syncfusion map-providers page).
                _layer.UrlTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
                break;
            case "google-urltemplate":
                // Placeholder — MainWindow fills after createSession in Loaded.
                _layer.UrlTemplate = "about:blank";
                break;
            case "google-geturi":
                _layer.LayerType = LayerType.OSM;
                break;
            default:
                _layer.LayerType = LayerType.OSM;
                break;
        }

        _map.Layers.Add(_layer);

        _status = new TextBlock
        {
            Margin = new Thickness(8),
            TextWrapping = TextWrapping.Wrap,
            Text = "Probing…",
        };

        Content = new DockPanel
        {
            Children =
            {
                _status.Also(t => DockPanel.SetDock(t, Dock.Top)),
                _map,
            },
        };

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_mode == "google-urltemplate" || _mode == "google-geturi")
            {
                var key = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
                if (string.IsNullOrWhiteSpace(key))
                {
                    Write("NO_KEY");
                    _status.Text = "NO_KEY";
                    return;
                }

                var template = await CreateGoogleTemplateAsync(key).ConfigureAwait(true);
                Write("session_ok template_host=tile.googleapis.com");
                if (_mode == "google-urltemplate")
                {
                    _layer.UrlTemplate = template;
                }
                else if (_layer is GoogleGetUriLayer g)
                {
                    g.SetTemplate(template);
                    g.LayerType = LayerType.Bing;
                    g.LayerType = LayerType.OSM;
                }
            }

            await Task.Delay(4000).ConfigureAwait(true);
            Inspect("after-4s");
        }
        catch (Exception ex)
        {
            Write("FAIL " + ex);
            _status.Text = ex.Message;
        }
    }

    private void Inspect(string label)
    {
        var field = typeof(ImageryLayer).GetField(
            "imageryPanel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var gate = typeof(ImageryLayer).GetField(
            "isTileGenerationInProgress",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var panel = field?.GetValue(_layer) as Panel;
        var children = panel?.Children.Count ?? -1;
        var withSource = 0;
        if (panel is not null)
        {
            foreach (UIElement child in panel.Children)
            {
                if (child is Tile tile && tile.TileImageSource is not null)
                {
                    withSource++;
                }
            }
        }

        var line =
            $"{label} mode={_mode} children={children} withSource={withSource} " +
            $"LayerType={_layer.LayerType} UrlTemplateEmpty={string.IsNullOrEmpty(_layer.UrlTemplate)} " +
            $"gate={gate?.GetValue(_layer)} size={_map.ActualWidth:0}x{_map.ActualHeight:0} " +
            $"center=({_layer.Center.X},{_layer.Center.Y})";
        Write(line);
        _status.Text = line;
    }

    private void Write(string line)
    {
        File.AppendAllText(_logPath, DateTime.Now.ToString("HH:mm:ss.fff ") + line + Environment.NewLine);
    }

    private static async Task<string> CreateGoogleTemplateAsync(string key)
    {
        using var http = new HttpClient();
        var url = "https://tile.googleapis.com/v1/createSession?key=" + Uri.EscapeDataString(key);
        using var content = new StringContent(
            """{"mapType":"roadmap","language":"en-US","region":"US"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        using var resp = await http.PostAsync(url, content).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("createSession " + (int)resp.StatusCode + " " + body);
        }

        // Minimal parse — Google may emit spaces after ':' in JSON.
        var match = System.Text.RegularExpressions.Regex.Match(
            body,
            "\"session\"\\s*:\\s*\"([^\"]+)\"");
        if (!match.Success)
        {
            throw new InvalidOperationException("no session in " + body);
        }

        var token = match.Groups[1].Value;
        return "https://tile.googleapis.com/v1/2dtiles/{z}/{x}/{y}?session="
            + Uri.EscapeDataString(token)
            + "&key="
            + Uri.EscapeDataString(key);
    }
}

internal sealed class GoogleGetUriLayer : ImageryLayer
{
    private string? _template;

    public void SetTemplate(string template) => _template = template;

    protected override string GetUri(int X, int Y, int Scale)
    {
        if (_template is null)
        {
            return string.Empty;
        }

        return _template
            .Replace("{z}", Scale.ToString(), StringComparison.Ordinal)
            .Replace("{x}", X.ToString(), StringComparison.Ordinal)
            .Replace("{y}", Y.ToString(), StringComparison.Ordinal);
    }
}

internal static class UiExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
