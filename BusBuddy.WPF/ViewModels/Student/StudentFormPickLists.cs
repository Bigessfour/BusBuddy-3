using BusBuddy.Core.Utilities;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Fixed pick lists for the student form's non-catalog dropdowns. These are constants, not data the
/// clerk maintains, so they live beside the form rather than in the database: a grade or a state
/// abbreviation is never a district-configurable value the way a route or a pickup stop is.
/// </summary>
internal static class StudentFormPickLists
{
    /// <summary>Grade pick list, shared with the students grid via <see cref="StudentGradeCatalog"/>.</summary>
    public static IReadOnlyList<string> Grades { get; } = StudentGradeCatalog.All;

    /// <summary>US state abbreviations, including DC. Postal order, not alphabetical by full name.</summary>
    public static IReadOnlyList<string> States { get; } =
    [
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA",
        "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
        "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
        "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
        "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY",
        "DC"
    ];
}
