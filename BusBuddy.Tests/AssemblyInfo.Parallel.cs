using NUnit.Framework;

// Run different test fixtures in parallel; tests within the same fixture remain sequential by default
[assembly: Parallelizable(ParallelScope.Fixtures | ParallelScope.Children)]  // Enable test-level if no shared state
[assembly: LevelOfParallelism(4)]  // Limit to 4 workers; adjust based on CI resources

// Tip: You can also control worker threads via RunSettings <NUnit><NumberOfTestWorkers>... in testsettings.runsettings
