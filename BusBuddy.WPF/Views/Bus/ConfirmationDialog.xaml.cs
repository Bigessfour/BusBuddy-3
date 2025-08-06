using System.Windows;
using System.Windows.Controls;

namespace BusBuddy.WPF.Views.Bus
{
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialog(string message, string title = "Confirmation")
        {
            InitializeComponent();
            Title = title;

            // Find the MessageText element and set its content
            if (FindName("MessageText") is TextBlock messageTextBlock)
            {
                messageTextBlock.Text = message;
            }
        }

        public ConfirmationDialog()
        {
            InitializeComponent();
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
    }
}
