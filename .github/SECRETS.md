# BusBuddy CI/CD Secrets

This project uses a simplified GitHub Actions CI for build/test/migrate/publish. Set these secrets to enable optional steps and avoid context access warnings.

Location: GitHub → Repo → Settings → Secrets and variables → Actions → New repository secret

Required secrets
- SYNCFUSION_LICENSE_KEY
  - Purpose: License registration for Syncfusion WPF controls.
  - Source: https://help.syncfusion.com/wpf/wpf-license-registration (from your Syncfusion account)
  - Notes: If missing, builds still succeed but a runtime license dialog may appear.

- AZURE_CREDENTIALS (preferred) OR the triplet below
  - Purpose: Azure login for ephemeral SQL firewall rules and DB operations.
  - Source: Create a service principal with Contributor to your subscription/resource group.
  - Format (JSON) per azure/login@v2:
    {
      "clientId": "<GUID>",
      "clientSecret": "<VALUE>",
      "subscriptionId": "<GUID>",
      "tenantId": "<GUID>"
    }
  - Docs: https://github.com/azure/login

- AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID (fallback if AZURE_CREDENTIALS not used)
  - Purpose: Alternative auth inputs for azure/login@v2
  - Source: Azure AD App Registration + subscription where BusBuddy resources exist

- BUSBUDDY_CONNECTION
  - Purpose: EF Core migration scripting and seeding.
  - Example: Server=busbuddy-server-sm2.database.windows.net;Database=BusBuddyDb;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;
  - Docs: https://learn.microsoft.com/ef/core/cli/dotnet

- AZURE_SQL_SERVER, AZURE_SQL_USER, AZURE_SQL_PASSWORD
  - Purpose: Apply migration scripts using sqlcmd.
  - Example: AZURE_SQL_SERVER=busbuddy-server-sm2.database.windows.net

Optional
- CODECOV_TOKEN (only if coverage uploads are enabled)

Validation
1) Manually run workflow: Actions → 🚌 BusBuddy CI Pipeline → Run workflow → Enable debug mode
2) Confirm these steps don’t fail due to missing secrets:
   - Configure Syncfusion license for build
   - Azure login (for SQL firewall)
   - Generate EF migration script
   - Apply EF migrations to Azure SQL (sqlcmd)
3) If you don’t use Azure yet, set only SYNCFUSION_LICENSE_KEY and skip DB steps — the pipeline gates those with conditions.

CLI helper (optional)
```powershell
# Set secrets quickly from terminal (replace placeholders)
gh secret set SYNCFUSION_LICENSE_KEY --app actions --body "<syncfusion-key>"
gh secret set BUSBUDDY_CONNECTION    --app actions --body "Server=tcp:...;Initial Catalog=BusBuddyDb;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
gh secret set AZURE_SQL_SERVER       --app actions --body "<server>.database.windows.net"
gh secret set AZURE_SQL_USER         --app actions --body "<sql-user>"
gh secret set AZURE_SQL_PASSWORD     --app actions --body "<sql-password>"
```

Notes on VS Code warnings
- You may see "Context access might be invalid" warnings for secrets in ci.yml. These are static analysis hints when secrets are not set in your local environment. Once the secrets exist in GitHub, the workflow will resolve them at runtime.
- We reduced secret references and duplicate steps to minimize noise, but warnings can still appear locally — this is expected and safe.
