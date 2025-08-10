@echo off
setlocal
REM Run WPF app with Azure SQL connection via environment variable overrides

set "ConnectionStrings__BusBuddyDb=Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;MultipleActiveResultSets=True;"
set "DatabaseProvider=Azure"

dotnet run --project BusBuddy.WPF\BusBuddy.WPF.csproj
endlocal
