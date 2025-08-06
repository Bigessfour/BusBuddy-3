using System.Windows.Controls;
using Serilog;

namespace BusBuddy.WPF.Controls
{
    /// <summary>
    /// Interaction logic for AddressValidationControl.xaml
    /// This is the XAML code-behind partial class.
    /// The main implementation is in AddressValidationControl.cs
    /// </summary>
    public partial class AddressValidationControl : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<AddressValidationControl>();

        // Event handlers and XAML-specific logic only
        // Main implementation is in AddressValidationControl.cs

        /// <summary>
        /// Event handler for the Validate Address button click
        /// </summary>
        private async void ValidateAddress_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                Logger.Information("Validate Address button clicked");

                // Get the address from the text box (find control by name)
                if (this.FindName("AddressTextBox") is TextBox addressTextBox &&
                    this.FindName("ResultsTextBlock") is TextBlock resultsTextBlock)
                {
                    var address = addressTextBox.Text.Trim();

                    if (string.IsNullOrEmpty(address))
                    {
                        resultsTextBlock.Text = "Please enter an address to validate.";
                        return;
                    }

                    // Update the address properties from the text box
                    Street = address;

                    // Call the validation method from the main implementation
                    await ValidateAddressAsync();
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex, "Error during address validation button click");
                if (this.FindName("ResultsTextBlock") is TextBlock resultsTextBlock)
                {
                    resultsTextBlock.Text = $"Error: {ex.Message}";
                }
            }
        }
    }
}
