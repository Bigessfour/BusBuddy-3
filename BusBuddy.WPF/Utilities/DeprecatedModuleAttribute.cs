using System;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Marks a module as deprecated. This is stronger than the standard [Obsolete] attribute
    /// as it will also apply visual styling in the UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class DeprecatedModuleAttribute : Attribute
    {
        /// <summary>
        /// Gets the message explaining why the module is deprecated.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the alternative module or feature that should be used instead.
        /// </summary>
        public string? AlternativePath { get; }

        /// <summary>
        /// Gets the planned removal date of this module, if any.
        /// </summary>
        public DateTime? PlannedRemovalDate { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeprecatedModuleAttribute"/> class.
        /// </summary>
        /// <param name="message">Message explaining why the module is deprecated.</param>
        /// <param name="alternativePath">Alternative module or feature to use instead.</param>
        /// <param name="plannedRemovalDate">Planned date for removal (optional).</param>
        public DeprecatedModuleAttribute(string message, string? alternativePath = null, string? plannedRemovalDate = null)
        {
            Message = message;
            AlternativePath = alternativePath;

            if (!string.IsNullOrEmpty(plannedRemovalDate) && DateTime.TryParse(plannedRemovalDate, out var date))
            {
                PlannedRemovalDate = date;
            }
        }
    }
}
