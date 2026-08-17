using System.Windows;
using System.Windows.Controls;
using Syncfusion.SfSkinManager;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.WPF.Views.Bus
{
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialog(string message, string title = "Confirmation")
        {
            InitializeComponent();
            Title = title;
            SyncfusionThemeManager.ApplyTheme(this);

            // Find the MessageText element and set its content
            if (FindName("MessageText") is TextBlock messageTextBlock)
            {
                messageTextBlock.Text = message;
            }
        }

        public ConfirmationDialog()
        {
            InitializeComponent();
            SyncfusionThemeManager.ApplyTheme(this);
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            try { SfSkinManager.Dispose(this); } catch { }
            base.OnClosed(e);
        }
    }
}
