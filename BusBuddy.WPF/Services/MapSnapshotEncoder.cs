using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;

namespace BusBuddy.WPF.Services;

/// <summary>
/// Encodes a live map visual to PNG bytes. Separate from <c>MapViewModel</c> camera/marker work.
/// </summary>
public static class MapSnapshotEncoder
{
    private static readonly ILogger Logger = Log.ForContext(typeof(MapSnapshotEncoder));

    public static byte[]? TryEncode(FrameworkElement mapElement, out string statusMessage)
    {
        if (mapElement is null)
        {
            statusMessage = "Map snapshot failed: element null";
            return null;
        }

        try
        {
            // Two device pixels per screen pixel so the printed map is not a stretched screenshot.
            const double printScale = 2d;
            var width = Math.Max(1, (int)Math.Round(mapElement.ActualWidth * printScale));
            var height = Math.Max(1, (int)Math.Round(mapElement.ActualHeight * printScale));

            var rtb = new RenderTargetBitmap(width, height, 96d * printScale, 96d * printScale, PixelFormats.Pbgra32);
            rtb.Render(mapElement);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            var bytes = ms.ToArray();
            Logger.Information("Captured map snapshot {Width}x{Height} bytes={Bytes}", width, height, bytes.Length);
            statusMessage = "Map snapshot captured";
            return bytes;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Map snapshot capture failed");
            statusMessage = "Map snapshot error";
            return null;
        }
    }
}
