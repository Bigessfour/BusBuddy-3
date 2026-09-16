using System;
using System.IO;
using System.Windows;
using Serilog;
using Syncfusion.Windows.Shared; // ChromelessWindow
using Syncfusion.Windows.PdfViewer; // PdfViewerControl API per Syncfusion docs

namespace BusBuddy.WPF.Views.Reports
{
    /// <summary>
    /// Internal PDF preview using Syncfusion PdfViewerControl.
    /// Documentation reference: https://help.syncfusion.com/wpf/pdf-viewer/getting-started and printing section.
    /// </summary>
    public partial class PdfPreviewWindow : ChromelessWindow
    {
        private static readonly ILogger Logger = Log.ForContext<PdfPreviewWindow>();
        private readonly string _filePath;

        private readonly string _windowTitle;

        public PdfPreviewWindow(byte[] pdfBytes, string? title = null)
        {
            ArgumentNullException.ThrowIfNull(pdfBytes);
            if (!IsPdfPayload(pdfBytes))
            {
                throw new ArgumentException("Bytes are not a PDF.", nameof(pdfBytes));
            }

            _windowTitle = string.IsNullOrWhiteSpace(title) ? "PDF" : title;
            _filePath = WriteViewerBackingFile(pdfBytes);
            try
            {
                InitializeComponent();
                Title = _windowTitle;
                Loaded += PdfPreviewWindow_Loaded;
                Closed += PdfPreviewWindow_Closed;
            }
            catch
            {
                TryDeleteBackingFile(_filePath);
                throw;
            }
        }

        /// <summary>%PDF- header check so the viewer is never handed HTML/error payloads.</summary>
        internal static bool IsPdfPayload(byte[] bytes)
        {
            if (bytes is not { Length: >= 5 })
            {
                return false;
            }

            return bytes[0] == (byte)'%'
                && bytes[1] == (byte)'P'
                && bytes[2] == (byte)'D'
                && bytes[3] == (byte)'F'
                && bytes[4] == (byte)'-';
        }

        private void PdfPreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    Logger.Warning("PDF backing file missing for preview");
                    MessageBox.Show("The PDF could not be opened.", _windowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Load(string) keeps the file in the viewer. Load(Stream) plus dispose races
                // PdfViewer's background VirtualizationList.FetchItemCount and can terminate the process.
                Viewer?.Load(_filePath);
                Logger.Information("PDF loaded into internal viewer");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed loading PDF into viewer");
                MessageBox.Show($"The PDF could not be opened: {ex.Message}", _windowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PdfPreviewWindow_Closed(object? sender, EventArgs e)
        {
            try
            {
                Viewer?.Unload(true);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "PdfViewer Unload on close failed");
            }

            TryDeleteBackingFile(_filePath);
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Viewer?.Print(true); // true = show print dialog per Syncfusion docs
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Print failed");
                MessageBox.Show($"Print failed: {ex.Message}", _windowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// PdfViewerControl requires a path (Load(Stream) is unsafe here). The file is a
        /// viewer backing store only — not a clerk-facing reports folder — and is deleted on close.
        /// </summary>
        private static string WriteViewerBackingFile(byte[] pdfBytes)
        {
            var dir = Path.Combine(Path.GetTempPath(), "BusBuddy");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"student-map-{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(path, pdfBytes);
            return path;
        }

        private static void TryDeleteBackingFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Temp PDF cleanup failed");
            }
        }
    }
}
