// Archived 2026-09. Not compiled. These tests locked EligibilityRoutePdfBuilder into MapViewModel.
// District Map print is the live snapshot (PrintRouteMapsCommand), not a second PDF writer.
// Original location: BusBuddy.Tests/WPF/MapViewModelTests.cs

#if false
    [Test]
    public async Task GenerateEligibilityRoutePdfAsync_NoStudents_ReturnsEmptyWithoutPlotting() { }

    [Test]
    public async Task GenerateEligibilityRoutePdfAsync_CatalogStopWithoutHome_IsIncludedAndDoesNotPlot() { }

    [Test]
    public async Task GenerateEligibilityRoutePdfAsync_UnvalidatedHomeOnly_IsSkipped() { }
#endif
