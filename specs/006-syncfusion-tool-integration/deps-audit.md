# Dependency audit — Spec 006 (2026-07-24)

Source: `dotnet list … package --outdated` + nuget.org flat container. Pins live in `Directory.Build.props`.

## Applied (this PR)

| Property                                          | Was         | Now             | Rationale                               |
| ------------------------------------------------- | ----------- | --------------- | --------------------------------------- |
| SyncfusionVersion                                 | 33.2.10     | **34.1.32**     | Latest Syncfusion WPF on nuget.org      |
| EntityFrameworkVersion                            | 9.0.8       | **9.0.18**      | Latest EF Core 9.x patch (stay on net9) |
| MicrosoftExtensionsVersion (+ DI/Hosting/Logging) | 9.0.8       | **9.0.18**      | Align with EF 9.0.18                    |
| SystemNetHttpJsonVersion                          | 9.0.8       | **9.0.18**      | Align Extensions 9.x                    |
| NpgsqlVersion                                     | 9.0.3       | **9.0.4**       | Latest Npgsql EF provider on 9.x        |
| SqlClientVersion                                  | 6.1.1       | **6.1.6**       | Latest 6.x (defer SqlClient 7)          |
| SerilogVersion                                    | 4.3.0       | **4.4.0**       | Latest Serilog 4.x                      |
| SerilogSinksConsoleVersion                        | 6.0.0       | **6.1.1**       | Latest console sink                     |
| WebView2Version                                   | 1.0.3405.78 | **1.0.4078.44** | Latest WebView2                         |
| GoogleApisVersion                                 | 1.70.0      | **1.75.0**      | GEE/auth stack                          |
| GoogleCloudStorageVersion                         | 4.13.0      | **4.15.0**      | GEE-related                             |
| GoogleApisDriveVersion                            | 1.70.0.3856 | **1.75.0.4210** | Match Google.Apis 1.75                  |
| MicrosoftIdentityClientVersion                    | 4.76.0      | **4.86.1**      | MSAL patch/minor                        |
| PollyVersion                                      | 8.6.3       | **8.7.0**       | Resilience minor                        |
| OpenAIVersion                                     | 2.3.0       | **2.12.0**      | Used by Core OpenAI-compatible client   |

## Deferred (explicit)

| Package                               | Current | Latest seen                                           | Why deferred                                                                                                     |
| ------------------------------------- | ------- | ----------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| AutoMapper (+ DI extensions)          | 12.0.1  | 15.x / 16.x (patched ≥15.1.1 for GHSA-rvv3-g6hj-g44x) | Major API + license/DI package changes; **risk accepted short-term** — track as P2 due-out to migrate or replace |
| Microsoft.EntityFrameworkCore *       | 9.0.18  | 10.0.10                                               | EF 10 implies broader stack move; stay on net9                                                                   |
| Microsoft.Extensions.*                | 9.0.18  | 10+/11 preview                                        | Keep aligned with EF 9                                                                                           |
| Npgsql.EntityFrameworkCore.PostgreSQL | 9.0.4   | 10.0.3                                                | Requires EF 10                                                                                                   |
| Microsoft.Data.SqlClient              | 6.1.6   | 7.x                                                   | Major; validate separately                                                                                       |
| Serilog.Extensions.Logging            | 9.0.2   | 10.0.0                                                | Major; app uses Serilog directly                                                                                 |
| CommunityToolkit.Mvvm                 | 8.4.2   | 8.4.2                                                 | Already current                                                                                                  |

## Verification

```bash
dotnet restore BusBuddy.sln -p:EnableWindowsTargeting=true
dotnet build BusBuddy.sln -c Release -p:EnableWindowsTargeting=true
```

Windows VM: Syncfusion license smoke after 34.x bump.
