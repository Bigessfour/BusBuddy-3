# BusBuddy Function Tree (overview)

High-level surface area. Due-outs: [action-items.md](./action-items.md). Generated scan: [function-inventory.generated.md](./function-inventory.generated.md).

```mermaid
flowchart TB
  subgraph agents [Agents]
    SpecKit[Spec-Kit skills]
    RAG[busbuddy-rag]
    SfMCP[Syncfusion WPF MCP]
    SfSkills[Syncfusion WPF skills]
  end
  subgraph ui [Operator UI]
    Students[StudentsView]
    Reports[ReportsView]
    Dashboard[DashboardView]
    Theme[SyncfusionThemeManager]
  end
  subgraph core [P1 Core]
    StudentSvc[StudentService]
    Seed[SeedDataService]
    RouteSvc[RouteService]
    Opt[StudentRouteOptimizer]
    ReportsSvc[OperationalReportService]
    Pdf[PdfReportService]
    DriverSvc[DriverService]
    Sched[ScheduleService]
  end
  subgraph p2 [P2]
    Maint[MaintenanceService]
    GEE[GoogleEarthEngineService]
    Metrics[DashboardMetricsService]
    Gcp[GcpCredentialBootstrap]
  end
  subgraph data [Data]
    EF[EF + Postgres Docker]
  end
  SpecKit --> core
  SpecKit --> ui
  RAG --> SpecKit
  SfMCP --> ui
  SfSkills --> ui
  Students --> StudentSvc
  Students --> Seed
  Students --> Opt
  Reports --> ReportsSvc
  ReportsSvc --> Pdf
  Dashboard --> Metrics
  Dashboard --> Opt
  Opt --> RouteSvc
  Theme --> ui
  core --> EF
  p2 --> EF
  GEE --> Gcp
```

Allowlist lives in [`.function-inventory.json`](../.function-inventory.json) (P1 first). Update this tree when a new operator view or core service is added to that list.
