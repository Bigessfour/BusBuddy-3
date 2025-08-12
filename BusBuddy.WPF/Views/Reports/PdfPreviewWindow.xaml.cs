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

        public PdfPreviewWindow(string filePath)
        {
            _filePath = filePath;
            InitializeComponent();
            Loaded += PdfPreviewWindow_Loaded;
        }

        private void PdfPreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    using var fs = File.OpenRead(_filePath);
                    Viewer?.Load(fs);
                    Logger.Information("PDF loaded into internal viewer: {File}", _filePath);
                }
                else
                {
                    Logger.Warning("PDF file not found for preview: {File}", _filePath);
                    MessageBox.Show("File not found", "PDF Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed loading PDF into viewer");
            }
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
                MessageBox.Show($"Print failed: {ex.Message}", "PDF Preview", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
