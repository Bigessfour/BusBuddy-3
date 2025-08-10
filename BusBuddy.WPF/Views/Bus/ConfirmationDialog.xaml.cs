using System.Windows;
using System.Windows.Controls;
using Syncfusion.SfSkinManager;

namespace BusBuddy.WPF.Views.Bus
{
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialog(string message, string title = "Confirmation")
        {
            InitializeComponent();
            Title = title;
            try
            {
                SfSkinManager.ApplyThemeAsDefaultStyle = true;
                using var dark = new Theme("FluentDark");
                SfSkinManager.SetTheme(this, dark);
            }
            catch
            {
                try { using var light = new Theme("FluentLight"); SfSkinManager.SetTheme(this, light); } catch { }
            }

            // Find the MessageText element and set its content
            if (FindName("MessageText") is TextBlock messageTextBlock)
            {
                messageTextBlock.Text = message;
            }
        }

        public ConfirmationDialog()
        {
            InitializeComponent();
            try
            {
                SfSkinManager.ApplyThemeAsDefaultStyle = true;
                using var dark = new Theme("FluentDark");
                SfSkinManager.SetTheme(this, dark);
            }
            catch
            {
                try { using var light = new Theme("FluentLight"); SfSkinManager.SetTheme(this, light); } catch { }
            }
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
