@echo off
setlocal
REM Query top 10 students using Azure CLI authentication
sqlcmd -S tcp:busbuddy-server-sm2.database.windows.net,1433 ^
       -d BusBuddyDB ^
       --authentication-method ActiveDirectoryAzCli ^
       -Q "SELECT TOP 10 * FROM dbo.Students ORDER BY 1;" ^
       -W -s ","
endlocal
