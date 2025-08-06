namespace BusBuddy.WPF.Models
{
    /// <summary>
    /// Represents filter status options for student searches.
    /// </summary>
    public enum FilterStatus
    {
        /// <summary>
        /// All items regardless of status.
        /// </summary>
        All,

        /// <summary>
        /// Only active items.
        /// </summary>
        Active,

        /// <summary>
        /// Only inactive items.
        /// </summary>
        Inactive,

        /// <summary>
        /// Items with a positive status.
        /// </summary>
        Yes,

        /// <summary>
        /// Items with a negative status.
        /// </summary>
        No,

        /// <summary>
        /// Items that have a route assigned.
        /// </summary>
        WithRoute,

        /// <summary>
        /// Items that do not have a route assigned.
        /// </summary>
        WithoutRoute,
    }
}
