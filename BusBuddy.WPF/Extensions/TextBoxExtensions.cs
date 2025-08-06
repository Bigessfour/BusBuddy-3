using System;
using System.Windows.Controls;

namespace BusBuddy.WPF.Extensions
{
    public static class TextBoxExtensions
    {

        /// <summary>
        /// Gets the Time value from a TextBox
        /// </summary>
        public static TimeSpan? GetTime(this TextBox textBox)
        {
            if (textBox == null)
            {
                return null;
            }


            if (textBox.Tag is TimeSpan timeSpan)
            {
                return timeSpan;
            }

            // Try to parse the text as a time
            if (TimeSpan.TryParse(textBox.Text, out TimeSpan result))
            {
                textBox.Tag = result;
                return result;
            }

            return null;
        }

        /// <summary>
        /// Sets the Time value on a TextBox
        /// </summary>
        public static void SetTime(this TextBox textBox, TimeSpan value)
        {
            if (textBox == null)
            {
                return;
            }


            textBox.Tag = value;
            textBox.Text = value.ToString(@"hh\:mm");
        }
    }
}
