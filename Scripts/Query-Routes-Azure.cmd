@echo off
setlocal
set SERVER=busbuddy-server-sm2.database.windows.net
set DATABASE=BusBuddyDB
set QUERY=SELECT TOP 10 RouteId, RouteName, Description, Date FROM Routes ORDER BY RouteId DESC;
sqlcmd -S %SERVER% -d %DATABASE% -G -Q "%QUERY%"
if %ERRORLEVEL% NEQ 0 echo Query failed. Ensure az login and sqlcmd are configured.
endlocal
