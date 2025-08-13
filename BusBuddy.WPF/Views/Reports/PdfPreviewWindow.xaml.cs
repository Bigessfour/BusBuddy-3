using System;
using System.IO;
using System.Windows;
using Serilog;
using Syncfusion.Windows.PdfViewer; // PdfViewerControl API per Syncfusion docs

namespace BusBuddy.WPF.Views.Reports
{
    /// <summary>
    /// Internal PDF preview using Syncfusion PdfViewerControl.
    /// Documentation reference: https://help.syncfusion.com/wpf/pdf-viewer/getting-started and printing section.
    /// </summary>
    public partial class PdfPreviewWindow : System.Windows.Controls.UserControl, BusBuddy.WPF.Views.Common.IDialogHostable
    {
        private static readonly ILogger Logger = Log.ForContext<PdfPreviewWindow>();
        private readonly string _filePath;
        public event EventHandler? RequestCloseByHost;
        public CommunityToolkit.Mvvm.Input.RelayCommand PrintCommand { get; }
        public CommunityToolkit.Mvvm.Input.RelayCommand CloseCommand { get; }
    public bool? DialogResult { get; private set; }

        public PdfPreviewWindow(string filePath)
        {
            _filePath = filePath;
            InitializeComponent();
            // Initialize commands prior to DataContext assignment (binding readiness best practice)
            PrintCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(PrintInternal);
            CloseCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => RequestCloseByHost?.Invoke(this, EventArgs.Empty));
            // Apply theme using centralized helper (Syncfusion WPF 30.1.42 pattern)
            BusBuddy.WPF.Utilities.SyncfusionThemeManager.ApplyTheme(this);
            DataContext = this;
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

        private void PrintInternal()
        {
            try
            {
                Viewer?.Print(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Print failed");
                MessageBox.Show($"Print failed: {ex.Message}", "PDF Preview", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void DisposeResources()
        {
            try
            {
                Loaded -= PdfPreviewWindow_Loaded;
                Logger.Information("Disposed resources for {View}", nameof(PdfPreviewWindow));
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error disposing resources for {View}", nameof(PdfPreviewWindow));
            }
        }
    }
}
