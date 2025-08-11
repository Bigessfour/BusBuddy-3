@echo off
setlocal
REM Query top 10 students using Azure AD (Entra ID) interactive auth via sqlcmd
REM Prereqs:
REM  - AZ CLI installed (az --version)
REM  - sqlcmd installed and on PATH (sqlcmd -?)
REM  - Run "az login" first if you haven't authenticated

set SERVER=busbuddy-server-sm2.database.windows.net
set DATABASE=BusBuddyDB
set QUERY=SELECT TOP 10 StudentId, StudentName, HomeAddress, Grade, School FROM Students ORDER BY StudentId DESC;

echo Querying Azure SQL Database: %DATABASE% on %SERVER%...
sqlcmd -S %SERVER% -d %DATABASE% -G -Q "%QUERY%"

if %ERRORLEVEL% NEQ 0 (
    echo Error: Query failed. Check authentication or run 'az login' first.
    echo Tip: Ensure sqlcmd is in your PATH and you have permissions.
)

pause
endlocal
