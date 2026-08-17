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
    MaintView[MaintenanceView]
    SchedView[DriverScheduleView]
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
    Avail[DriverAvailabilityCalculator]
    Maint[MaintenanceService]
  end
  subgraph p2 [P2 Geo]
    Geo[GeoDataService]
    MapsValidate[GoogleAddressValidationClient]
    MapsRoute[GoogleRoutingService]
    Metrics[DashboardMetricsService]
    Shape[ShapefileEligibilityService]
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
  Students --> MapsValidate
  Reports --> ReportsSvc
  ReportsSvc --> Pdf
  Dashboard --> Metrics
  Dashboard --> Opt
  MaintView --> Maint
  SchedView --> Sched
  SchedView --> Avail
  Opt --> RouteSvc
  Theme --> ui
  MapsRoute --> Geo
  core --> EF
  p2 --> EF
```

Allowlist lives in [`.function-inventory.json`](../.function-inventory.json) (P1 first). Update this tree when a new operator view or core service is added to that list.
