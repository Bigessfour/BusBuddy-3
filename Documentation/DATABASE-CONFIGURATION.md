# Database Configuration

BusBuddy supports local SQL Server / LocalDB, SQLite, and **Postgres via Docker** (recommended for hybrid Mac + VM dev).

## Priority order

1. **`BUSBUDDY_CONNECTION` environment variable** (highest — overrides appsettings)
2. **`DatabaseProvider`** in appsettings + matching connection string
3. LocalDB fallback when placeholders are unresolved

## Providers

| Provider | When to use | Connection key |
|----------|-------------|----------------|
| `LocalDB` / `SqlServer` | Windows VM, SQL Express / LocalDB | `LocalConnection` or `DefaultConnection` |
| `Postgres` | Mac Docker (`docker compose --profile db up -d`) | `PostgresConnection` or `BUSBUDDY_CONNECTION` |
| `Local` | SQLite file | `BusBuddyDatabase` |

## Postgres (Mac Docker → VM)

On Mac:

```bash
docker compose --profile db up -d
```

From Windows VM (use Mac host IP from `./run-wpf.sh`):

```powershell
$env:BUSBUDDY_CONNECTION = "Host=192.168.x.x;Port=5432;Database=busbuddy_test;Username=busbuddy;Password=busbuddy_dev"
```

## Local Windows VM default

`BusBuddy.WPF/appsettings.json` defaults to SQL Server Express on `localhost\SQLEXPRESS`.

## Migrations (design-time)

Postgres (Mac Docker) is the hybrid-dev path. From the repo root:

```bash
docker compose --profile db up -d
export BUSBUDDY_CONNECTION="Host=localhost;Port=5432;Database=busbuddy_migrate;Username=busbuddy;Password=busbuddy_dev"
# Use a catalog that was not created with EnsureCreated (no __EFMigrationsHistory).
docker compose --profile db exec -T postgres psql -U busbuddy -d postgres -c "CREATE DATABASE busbuddy_migrate;"
dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.Core
```

`EnableWindowsTargeting` is already set in `Directory.Build.props`. Migrations are provider-aware (`MigrationSql`) so the same chain applies on SQL Server and Postgres.

The WPF app applies the same chain at startup via `RelationalSchemaApplier` (`Database.Migrate()`), not `EnsureCreated()`. Catalogs created with `EnsureCreated()` have tables but no `__EFMigrationsHistory` — the app will refuse to start against those. Drop/recreate that catalog or use a new database name.

Windows VM SQL Server still works with the same `dotnet ef database update` command when `BUSBUDDY_CONNECTION` is a SQL Server string.
