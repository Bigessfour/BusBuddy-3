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

}
