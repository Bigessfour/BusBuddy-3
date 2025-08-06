using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BusBuddy.WPF.Models
{
    /// <summary>
    /// Enum representing time slots for bus routes
    /// </summary>
    public enum TimeSlot
    {
        [Description("Morning")]
        [Display(Name = "AM")]
        AM = 0,

        [Description("Afternoon")]
        [Display(Name = "PM")]
        PM = 1,

        [Description("Mid-Day")]
        [Display(Name = "Mid-Day")]
        MidDay = 2,

        [Description("Evening")]
        [Display(Name = "Evening")]
        Evening = 3,

        [Description("Weekend")]
        [Display(Name = "Weekend")]
        Weekend = 4,

        [Description("Special Event")]
        [Display(Name = "Special")]
        Special = 5,

        [Description("Field Trip")]
        [Display(Name = "Field Trip")]
        FieldTrip = 6
    }

    /// <summary>
    /// Extension methods for TimeSlot enum
    /// </summary>
    public static class TimeSlotExtensions
    {
        /// <summary>
        /// Gets the display name for a TimeSlot enum value
        /// </summary>
        public static string GetDisplayName(this TimeSlot timeSlot)
        {
            var fieldInfo = timeSlot.GetType().GetField(timeSlot.ToString());
            if (fieldInfo == null)
            {

                return timeSlot.ToString();
            }


            var attributes = (DisplayAttribute[])fieldInfo.GetCustomAttributes(typeof(DisplayAttribute), false);

            return attributes.Length > 0 ? attributes[0].Name ?? timeSlot.ToString() : timeSlot.ToString();
        }

        /// <summary>
        /// Gets the description for a TimeSlot enum value
        /// </summary>
        public static string GetDescription(this TimeSlot timeSlot)
        {
            var fieldInfo = timeSlot.GetType().GetField(timeSlot.ToString());
            if (fieldInfo == null)
            {

                return timeSlot.ToString();
            }


            var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

            return attributes.Length > 0 ? attributes[0].Description : timeSlot.ToString();
        }
    }
}
