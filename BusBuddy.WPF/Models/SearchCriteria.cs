namespace BusBuddy.WPF.Models
{
    /// <summary>
    /// Represents search criteria for advanced student filtering.
    /// </summary>
    public class SearchCriteria
    {
        /// <summary>
        /// Gets or sets the student name filter.
        /// </summary>
        public string StudentName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the student number filter.
        /// </summary>
        public string StudentNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the grade filter.
        /// </summary>
        public string? Grade { get; set; }

        /// <summary>
        /// Gets or sets the school filter.
        /// </summary>
        public string? School { get; set; }

        /// <summary>
        /// Gets or sets the active status filter.
        /// </summary>
        public FilterStatus ActiveStatus { get; set; }

        /// <summary>
        /// Gets or sets the special needs status filter.
        /// </summary>
        public FilterStatus SpecialNeedsStatus { get; set; }

        /// <summary>
        /// Gets or sets the AM route filter.
        /// </summary>
        public string? AMRoute { get; set; }

        /// <summary>
        /// Gets or sets the PM route filter.
        /// </summary>
        public string? PMRoute { get; set; }

        /// <summary>
        /// Gets or sets the bus stop filter.
        /// </summary>
        public string BusStop { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the route assignment status filter.
        /// </summary>
        public FilterStatus RouteAssignmentStatus { get; set; }

        /// <summary>
        /// Gets or sets the city filter.
        /// </summary>
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ZIP code filter.
        /// </summary>
        public string Zip { get; set; } = string.Empty;
    }
}
