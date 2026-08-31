# BusBuddy Function Tree (overview)

High-level surface area. Due-outs: [action-items.md](./action-items.md). Clerk write path: [clerk-path.md](./clerk-path.md). Generated scan: [function-inventory.generated.md](./function-inventory.generated.md).

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
    RouteAssign[RouteAssignmentView]
    Dashboard[DashboardView]
    MaintView[MaintenanceView]
    SchedView[DriverScheduleView]
    MapView[MapView]
    Theme[SyncfusionThemeManager]
  end
  subgraph core [P1 Core]
    StudentSvc[StudentService]
    Seed[SeedDataService]
    RouteSvc[RouteService]
    RouteDet[RouteDetermination]
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
  MapView --> Geo
  MapView --> MapsValidate
  RouteAssign --> RouteDet
  Reports --> ReportsSvc
  ReportsSvc --> Pdf
  Dashboard --> Metrics
  Dashboard --> Opt
  MaintView --> Maint
  SchedView --> Sched
  SchedView --> Avail
  Opt --> RouteSvc
  RouteDet --> RouteSvc
  Theme --> ui
  MapsRoute --> Geo
  core --> EF
  p2 --> EF
```

Allowlist lives in [`.function-inventory.json`](../.function-inventory.json) (P1 first). Update this tree when a new operator view or core service is added to that list.
