using NUnit.Framework;

// Run different test fixtures in parallel; tests within the same fixture remain sequential by default
[assembly: Parallelizable(ParallelScope.Fixtures)]

// Tip: You can also control worker threads via RunSettings <NUnit><NumberOfTestWorkers>... in testsettings.runsettings
