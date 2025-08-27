# Azure SQL Database Setup for BusBuddy-3 GitHub Integration

## ✅ Completed Steps

1. **Azure AD App Registration Created**
    - App Name: BusBuddy-3
    - Client ID: `860af3d3-df7a-4c76-915a-a6f980bd86ed`
    - Tenant ID: `3ee44d11-b5ae-43a0-9c02-004b04858d9e`
    - Service Principal Object ID: `0a93d214-37e7-4147-beaf-8ca8036c614e`

2. **Resource Group Role Assignment**
    - Assigned Contributor role to BusBuddy-3 service principal for BusBuddy-RG

## 📋 Manual Steps Required

### 1. Run SQL Script in Both Databases

**Databases to update:**

- BusBuddyDB (Production)
- BusBuddyDB-Staging (Staging)

**Steps:**

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to each database
3. Open **Query editor (preview)**
4. Sign in with your Entra ID admin account
5. Run the script from `setup-database-user.sql`

### 2. Add GitHub Secrets

Go to your GitHub repository → Settings → Secrets and variables → Actions

Add these secrets:

- **AZURE_CLIENT_ID**: `860af3d3-df7a-4c76-915a-a6f980bd86ed`
- **AZURE_TENANT_ID**: `3ee44d11-b5ae-43a0-9c02-004b04858d9e`
- **AZURE_CLIENT_SECRET**: [From credential reset output - see terminal]

### 3. Enable GitHub Workflow

The workflow file has been created at `.github/workflows/azure-sql-ci-cd.yml`

**Features:**

- Builds and tests on push/PR
- Deploys migrations to staging on develop branch
- Deploys migrations to production on main/master branch
- Uses Azure AD Service Principal authentication

### 4. Test the Setup

1. Commit and push changes to develop branch
2. Check GitHub Actions for successful execution
3. Verify database migrations run correctly

## 🔐 Security Notes

- Client secret should be rotated regularly
- Monitor service principal permissions
- Use staging environment for testing
- Ensure connection strings use encryption

## 🚨 Troubleshooting

If migrations fail:

1. Check service principal permissions
2. Verify database user was created correctly
3. Check firewall rules allow GitHub Actions IPs
4. Validate connection string format

## 📞 Support

If you encounter issues:

1. Check Azure Portal for service principal status
2. Verify database user exists and has correct roles
3. Test connection string locally first
