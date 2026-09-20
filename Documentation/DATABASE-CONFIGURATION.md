# Database Configuration

BusBuddy’s database is **Postgres** (Mac Docker → UTM guest). SQL Server / LocalDB and SQLite remain explicit opt-in only.

## Priority order

1. **`BUSBUDDY_CONNECTION`** (highest — a `Host=` string is Postgres)
2. **`DatabaseProvider`** in appsettings (`Postgres` by default) + matching connection string
3. Local Docker Postgres (`Host=localhost`) when no env override is set

Launchers (`utm_run_in_vm.ps1`, `run-wpf.sh`) write `BUSBUDDY_CONNECTION` only. The app calls `PostgresConnectionResolver.ResolveAndApply()` at startup.

Environment variables override JSON ([ASP.NET Core configuration](https://learn.microsoft.com/aspnet/core/fundamentals/configuration)).

## Providers

| Provider                | When to use                         | Connection key                                |
| ----------------------- | ----------------------------------- | --------------------------------------------- |
| `Postgres`              | Default. Mac Docker → UTM guest     | `PostgresConnection` or `BUSBUDDY_CONNECTION` |
| `LocalDB` / `SqlServer` | Explicit SQL Express / LocalDB only | `LocalConnection` or `DefaultConnection`      |
| `Local`                 | SQLite file                         | `BusBuddyDatabase`                            |

Unknown or empty `DatabaseProvider` is Postgres.

## Postgres (Mac Docker → VM)

On Mac:

```bash
docker compose --profile db up -d
```

From Windows VM (use Mac host IP from `./run-wpf.sh`):

```powershell
$env:BUSBUDDY_CONNECTION = "Host=192.168.x.x;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=busbuddy_dev"
```

## Opt-in SQL Server

Set `DatabaseProvider` to `SqlServer` or `LocalDB` and do not set a `Host=` `BUSBUDDY_CONNECTION`. `DefaultConnection` in appsettings is SQL Express for that switch only.

## Migrations (design-time)

From the repo root:

```bash
docker compose --profile db up -d
export BUSBUDDY_CONNECTION="Host=localhost;Port=5432;Database=busbuddy_migrate;Username=busbuddy;Password=busbuddy_dev"
# Use a catalog that was not created with EnsureCreated (no __EFMigrationsHistory).
docker compose --profile db exec -T postgres psql -U busbuddy -d postgres -c "CREATE DATABASE busbuddy_migrate;"
dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.Core
```

`EnableWindowsTargeting` is already set in `Directory.Build.props`. Migrations are provider-aware (`MigrationSql`) so the same chain applies on SQL Server and Postgres.

The WPF app applies the same chain at startup via `RelationalSchemaApplier` (`Database.Migrate()`), not `EnsureCreated()`. Catalogs created with `EnsureCreated()` have tables but no `__EFMigrationsHistory` — the app will refuse to start against those. Drop/recreate that catalog or use a new database name.

Windows VM SQL Server still works with the same `dotnet ef database update` command when `BUSBUDDY_CONNECTION` is a SQL Server string and `DatabaseProvider` is `SqlServer`.
