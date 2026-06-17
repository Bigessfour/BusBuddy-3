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

```bash
dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.WPF
```

Uses `BUSBUDDY_CONNECTION` if set; otherwise LocalDB.
