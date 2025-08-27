using System;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Standardized test categories for BusBuddy test organization
    /// Use these categories to group related tests for better organization and filtering
    /// </summary>
    public static class TestCategories
    {
        // Core functionality categories
        public const string Unit = "Unit";
        public const string Integration = "Integration";
        public const string Database = "Database";

        // Domain-specific categories
        public const string Routes = "Routes";
        public const string Students = "Students";
        public const string Buses = "Buses";
        public const string Drivers = "Drivers";
        public const string Activities = "Activities";

        // Business logic categories
        public const string BusinessRules = "BusinessRules";
        public const string Validation = "Validation";
        public const string Services = "Services";

        // UI/ViewModel categories
        public const string ViewModels = "ViewModels";
        public const string UI = "UI";
        public const string WPF = "WPF";

        // Performance and load categories
        public const string Performance = "Performance";
        public const string Load = "Load";

        // Special test types
        public const string Smoke = "Smoke";
        public const string Regression = "Regression";
        public const string EndToEnd = "EndToEnd";

        // Data categories
        public const string DataSeeding = "DataSeeding";
        public const string DataMigration = "DataMigration";

        // External dependencies
        public const string Azure = "Azure";
        public const string ExternalAPI = "ExternalAPI";
    }
}
