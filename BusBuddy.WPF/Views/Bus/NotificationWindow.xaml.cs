using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace BusBuddy.WPF.Views.Bus
{
    public partial class NotificationWindow : Window
    {
        public enum NotificationType
        {
            Success,
            Error,
            Warning,
            Information
        }

        public NotificationWindow()
        {
            InitializeComponent();
        }

        public NotificationWindow(string message, string title = "Notification", NotificationType type = NotificationType.Information)
        {
            InitializeComponent();

            // Find elements by name and set their properties
            if (FindName("TitleText") is TextBlock titleText)
            {
                titleText.Text = title;
            }

            if (FindName("MessageText") is TextBlock messageText)
            {
                messageText.Text = message;
            }

            // Set colors based on notification type
            if (FindName("MainBorder") is Border mainBorder)
            {
                switch (type)
                {
                    case NotificationType.Success:
                        mainBorder.BorderBrush = new SolidColorBrush(Colors.Green);
                        if (FindName("TitleText") is TextBlock titleTextSuccess)
                        {
                            titleTextSuccess.Foreground = new SolidColorBrush(Colors.Green);
                        }

                        break;
                    case NotificationType.Error:
                        mainBorder.BorderBrush = new SolidColorBrush(Colors.Red);
                        if (FindName("TitleText") is TextBlock titleTextError)
                        {
                            titleTextError.Foreground = new SolidColorBrush(Colors.Red);
                        }

                        break;
                    case NotificationType.Warning:
                        mainBorder.BorderBrush = new SolidColorBrush(Colors.Orange);
                        if (FindName("TitleText") is TextBlock titleTextWarning)
                        {
                            titleTextWarning.Foreground = new SolidColorBrush(Colors.Orange);
                        }

                        break;
                    case NotificationType.Information:
                        mainBorder.BorderBrush = new SolidColorBrush(Colors.Blue);
                        if (FindName("TitleText") is TextBlock titleTextInfo)
                        {
                            titleTextInfo.Foreground = new SolidColorBrush(Colors.Blue);
                        }

                        break;
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
