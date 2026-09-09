# BusBuddy-3 Architecture Map

Canonical repo architecture diagram. Update this file when services, CI jobs, or auth flows change; then run `python -m rag.index`.

**Ship tracker (open work):** [docs/action-items.md](../../docs/action-items.md)  
**Historical finish narrative (archived):** [../Archive/2026-06-Steady-State-Finish/STEADY-STATE-AND-FINISH-ROADMAP.md](../Archive/2026-06-Steady-State-Finish/STEADY-STATE-AND-FINISH-ROADMAP.md)

Optional editable Mermaid source: `busbuddy-3-architecture.mmd` in this folder if present.

## How to read the map

| Color  | Layer                                                       |
| ------ | ----------------------------------------------------------- |
| Blue   | Core — data, models, services                               |
| Orange | WPF — ViewModels and Syncfusion views                       |
| Green  | Infrastructure — Docker, CI/CD, databases, dev environments |
| Purple | Tests — BusBuddy.Tests proof coverage                       |

**Key flows:**

- **Mac hybrid dev**: Core/tests/Docker on Mac; full WPF in Windows VM (UTM/Parallels).
- **Data**: Services → Repositories → DbContext → Postgres (Docker) or SQL Server (prod) or InMemory (tests).
- **CI**: `feature/*` PR → Build & Test → auto-merge squash → Release artifacts on `master` push.
- **Local gate**: `.github/scripts/validate-ci-local.sh` mirrors Docker + compile before push.

## 1. System architecture

```mermaid
flowchart TB

  subgraph DEV["Development Environments"]
    direction LR
    MAC["MacBook Pro<br/>VS Code + Dev Container<br/>dotnet + Docker"]
    WSL["WSL / bash helpers"]
    WINVM["Windows 11 VM / UTM<br/>Full WPF + Syncfusion"]
    KEYS["macOS Passwords<br/>XAI / Syncfusion keys"]
    MAC --> WSL
    MAC --> WINVM
    KEYS -.-> MAC
  end

  subgraph SOLUTION["BusBuddy.sln - .NET 9"]
    direction TB
    CORE["BusBuddy.Core<br/>net9.0-windows"]
    WPF["BusBuddy.WPF<br/>Syncfusion UI"]
    TESTS["BusBuddy.Tests<br/>NUnit + Moq"]
    WPF --> CORE
    TESTS --> CORE
    TESTS --> WPF
  end

  subgraph DATA["Data Layer - BusBuddy.Core/Data"]
    direction TB
    CTX["BusBuddyDbContext"]
    FACT["DbContextFactory"]
    UOW["UnitOfWork"]
    REPOS["Repositories<br/>Student Route Bus Driver<br/>Vehicle Fuel Maintenance Activity"]
    CTX --> REPOS
    FACT --> CTX
    UOW --> REPOS
  end

  subgraph MODELS["Domain Models"]
    direction LR
    M1["Student Family Guardian"]
    M2["Route RouteStop Assignment"]
    M3["Bus Vehicle Driver"]
    M4["Maintenance Fuel Activity Schedule"]
    M5["AIInsight GeoAnalysis"]
  end

  subgraph SERVICES["Core Services"]
    direction TB
    S_SEED["SeedDataService"]
    S_STU["StudentService"]
    S_RTE["RouteService"]
    S_BUS["BusService"]
    S_DRV["DriverService"]
    S_FLT["FleetMonitoringService"]
    S_MNT["MaintenanceService"]
    S_FUEL["FuelService"]
    S_PDF["PdfReportService"]
    S_DASH["DashboardMetricsService"]
    S_GEO["GeoDataService<br/>Address Validation + SfMap<br/>Maps Platform"]
    S_AI["GrokGlobalAPI<br/>AIInsightService"]
    S_USR["UserContextService<br/>UserSettingsService"]
    S_ADDR["AddressValidationService"]
    S_ROUTEDET["RouteDeterminationService<br/>DensityCell + Packer + Fitness"]
  end

  subgraph UI["WPF ViewModels and Views"]
    direction TB
    VM_DASH["Dashboard"]
    VM_STU["Student"]
    VM_RTE["Route Assignment / Generate Routes"]
    VM_DRV["Drivers"]
    VM_BUS["Bus / Vehicle"]
    VM_RPT["Reports"]
    VM_ANA["Analytics"]
    VM_FUEL["Fuel"]
    VM_ACT["Activity"]
    VM_GEO["Map SfMap"]
    VM_SET["Settings"]
    VIEWS["Syncfusion Views<br/>SfDataGrid SfMap SfChart"]
    VM_DASH --> VIEWS
    VM_STU --> VIEWS
    VM_RTE --> VIEWS
  end

  subgraph DB["Database Providers"]
    direction LR
    PG[("Postgres 16<br/>busbuddy_test")]
    SQL[("SQL Server<br/>prod option")]
    MEM[("EF InMemory<br/>unit tests")]
  end

  subgraph DOCKER["Docker Compose Profiles"]
    direction TB
    D_PG["profile db<br/>postgres service"]
    D_TEST["profile test<br/>busbuddy-test image"]
    D_DEV["profile dev<br/>sleep infinity"]
    D_PG --> PG
    D_TEST --> CORE
  end

  subgraph CICD["CI/CD - GitHub Actions"]
    direction TB
    PR["feature branch PR"]
    GATE1["Build and Test<br/>windows-latest"]
    AM["Auto-merge<br/>squash on green"]
    REL["Release artifacts<br/>win-x64 WPF publish"]
    DOCKSIM["Docker CI sim<br/>manual workflow"]
    PR --> GATE1
    GATE1 --> AM
    AM --> REL
  end

  subgraph TOOLS["Agent and Dev Tooling"]
    direction LR
    RAG["busbuddy-rag MCP<br/>search_repo_context"]
    SYNC["Syncfusion WPF Assistant"]
    TRUNK["Trunk lint"]
    VAL["validate-ci-local.sh"]
    DOCS["AGENTS.md<br/>copilot-instructions"]
  end

  subgraph ARCHIVE["Documentation/Archive"]
    direction LR
    A1["Legacy PS modules"]
    A2["MVP Phase scaffolding"]
    A3["VM Docker hygiene"]
  end

  subgraph TESTPROOF["BusBuddy.Tests/Core"]
    direction LR
    T1["SeedDataServiceTests"]
    T2["StudentServiceTests"]
    T3["RouteServiceTests"]
    T4["MaintenanceServiceTests"]
    T5["PdfReportServiceTests"]
    T6["FleetMonitoringServiceTests"]
    T7["GapsCoverageTests"]
    T8["Driver Family Guardian"]
  end

  DEV --> SOLUTION
  CORE --> DATA
  CORE --> MODELS
  CORE --> SERVICES
  SERVICES --> DATA
  WPF --> UI
  UI --> SERVICES
  DATA --> DB
  DOCKER --> DB
  CICD --> TESTS
  VAL --> DOCKER
  VAL --> TESTS
  TOOLS --> DEV
  SERVICES --> S_AI
  SERVICES --> S_GEO
  TESTS --> TESTPROOF
  TESTPROOF --> SERVICES
  ARCHIVE -.-> CORE

  classDef core fill:#e8f4fc,stroke:#1a73e8
  classDef ui fill:#fff3e0,stroke:#f57c00
  classDef infra fill:#e8f5e9,stroke:#2e7d32
  classDef test fill:#f3e5f5,stroke:#7b1fa2
  class CORE,DATA,SERVICES,MODELS core
  class WPF,UI ui
  class DOCKER,CICD,DB,DEV infra
  class TESTS,TESTPROOF test
```

## 2. Solo-dev CI/CD sequence

```mermaid
sequenceDiagram
    autonumber
    actor Dev as Solo Developer
    participant Local as validate-ci-local.sh
    participant Docker as Docker Compose
    participant GH as GitHub PR
    participant CI as CI Workflow
    participant AM as Auto-merge
    participant Master as master branch

    Dev->>Local: Run before push
    Local->>Docker: build Core image + Postgres health
    Local->>Local: dotnet restore + build
    Note over Local: Skip WPF tests on Mac
    Dev->>GH: Push feature branch + open PR
    GH->>CI: Trigger Build and Test
    CI-->>GH: Required checks pass
    GH->>AM: Enable squash auto-merge
    AM->>Master: Squash merge when gates green
    Master->>CI: Push triggers Release artifacts
```

## 3. Directory quick reference

| Path                                   | Role                                             |
| -------------------------------------- | ------------------------------------------------ |
| `BusBuddy.Core/`                       | Domain models, EF data layer, business services  |
| `BusBuddy.WPF/`                        | Syncfusion UI, ViewModels, App.xaml.cs DI + keys |
| `BusBuddy.Tests/Core/`                 | Service-level proof tests (CI filter target)     |
| `docker-compose.yml`                   | Postgres + test/dev profiles                     |
| `Dockerfile`                           | Linux Core build image                           |
| `.github/workflows/ci.yml`             | Merge gate: Build & Test                         |
| `.github/workflows/auto-merge.yml`     | Squash auto-merge on green                       |
| `.github/scripts/validate-ci-local.sh` | Pre-push local validation                        |
| `Documentation/Archive/`               | Legacy PS, MVP, hygiene archives                 |
| `docs/action-items.md`                 | Ship SSOT (open work only)                       |
| `rag/`                                 | Semantic RAG index for agent context             |
| `AGENTS.md`                            | Agent quick reference                            |
| `.github/copilot-instructions.md`      | Full AI dev standards + CI rules                 |

_Extracted from STEADY-STATE architecture section 2026-09-09. Re-validate Mermaid after major structural changes._
