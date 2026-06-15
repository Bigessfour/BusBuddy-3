# Final Portfolio Baseline - Legacy Cleanse (2026-06)

**Context**: BusBuddy (school transportation management) - first programming project with many iterations. Preparing clean, functional baseline for Cloud Resume Challenge portfolio submission.

**Review Criteria for "No Future / Archive"**:
- Explicitly marked MVP/Phase/deprecated/legacy/stub/"coming soon"/"in development" with no production path.
- Superseded by newer implementations (e.g., Phase seeders replaced by SeedDataService; old appointment models by Activity/Schedule; debug-only services).
- Historical plans/reports/docs with no ongoing value (MVP-to-FA plans, old cleanup reports, non-essential one-offs).
- Code that would not ship in a streamlined, Docker/Postgres-focused, testable production version (SQL Server as sole primary is de-emphasized for cloud testing; pure dev/debug artifacts).
- Residual first-attempt clutter after prior hygiene passes (PS purge, VM dedup, root cleanup).

**What was archived in this pass** (git mv for traceability; full history preserved):
- Phase/MVP scaffolding: Phase1DataSeedingService.cs, Phase2DataSeederService.cs, Phase1StartupExtensions.cs
- Deprecated importers/loaders: JsonDataImporter.cs, EnhancedDataLoaderService.cs
- Legacy models: Legacy.cs, BusBuddyScheduleAppointment*.cs, IScheduleAppointment*.cs, SportsEvent.cs (and related Sports ViewModels in WPF)
- Debug/dev-only: DatabaseDebuggingInterceptor.cs, DatabaseNullFixService.cs (and migration), DatabasePerformanceOptimizer.cs, DataIntegrityService.cs (WPF + Core + tests), EFCoreDebuggingService.cs
- "InDevelopment" placeholders: BaseInDevelopmentViewModel.cs + inheritors (Activity*ViewModels in WPF)
- Stubby/legacy ViewModels and services: Sports*ViewModels, some Route stubs, old BusBuddySchedule* providers
- Legacy test dirs: Phase3Tests/ (naming + DataIntegrity mocks)
- Historical/non-production docs: Additional one-off plans, old DB config focused purely on LocalDB/SQL, remaining MVP language references where they polluted active paths.
- Any final scattered residuals (specific old reports, examples if non-referenced).

**What remains as the promoted baseline (functional, has future)**:
- Core domain: Models (Bus, Route, Student, Driver, Maintenance, Fuel, Activity/Schedule, Family/Guardian, AIInsight, etc.), Repos/UoW, SeedDataService (with real data), Student/Route/Bus/Driver/Fuel/Maintenance services + tests.
- Postgres/Docker path (primary for testing/portfolio): Full support in DbContext/Factory, docker-compose profiles (db + test), EnsureCreated for dev, CURRENT_TIMESTAMP defaults.
- Multi-provider kept for flexibility (SQL Server compat for Windows/VM prod options) but docs now position Postgres + Docker as the cloud/resume-friendly story.
- Functional features: PdfReportService + tests, GrokGlobalAPI (route opt), Dashboard/FleetMonitoring, Address/Geo services, basic assignment flows.
- UI: Syncfusion WPF (clean Views/ViewModels for core entities; stubs for future features explicitly noted or archived).
- Infrastructure: Docker (Postgres for tests, .NET 9 Linux image for Core isolation), CI (license/vuln/coverage/CodeQL), Scripts/Validate-Dependencies, .devcontainer, hybrid Mac+UTM dev notes.
- Tests: Core service tests proving "it works" (Seed, Student, Route, Maintenance, PdfReport, etc.).
- Docs: README, STEADY-STATE-AND-FINISH-ROADMAP (updated with this baseline), DEVELOPMENT-GUIDE (Docker/VM focus), essential references.

**Portfolio Readiness Notes**:
- This cleanse sets the "shippable" baseline. Only code that demonstrates solid .NET, EF (Postgres in Docker for cloud demo), services, testing, AI integration, clean architecture, and modern dev practices remains in the main tree.
- Archived items are available in git history for "first attempt" story if desired in resume narrative, but not promoted.
- Remaining TODOs/stubs are limited to true "future" features (auth, full GoogleEarth polish, advanced reports) — not MVP placeholders.
- Build: `dotnet build BusBuddy.sln -p:EnableWindowsTargeting=true` (Core/Tests green on Mac; full WPF on VM/Windows).
- Test DB: `docker compose --profile db up -d` (Postgres healthy, busbuddy_test).
- Next steps per roadmap: close key finish items with tests, ensure coverage gate, prod deploy notes.

Generated as the final hygiene action. All moves via git for full provenance.
