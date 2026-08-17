@echo off
title BusBuddy
REM Main sync + rebuild + launch. Called by C:\dev\BusBuddy-Launch\Run-BusBuddy.cmd
REM (desktop icon). Always prefer share for latest Mac sources.

set "LOCAL_ROOT=C:\dev\BusBuddy-3"
set "SHARE_ROOT="

if exist "Z:\BusBuddy.sln" set "SHARE_ROOT=Z:\"
if not defined SHARE_ROOT if exist "Z:\Shared with Windows\BusBuddy.sln" set "SHARE_ROOT=Z:\Shared with Windows\"
if not defined SHARE_ROOT if exist "Z:\BusBuddy-3\BusBuddy.sln" set "SHARE_ROOT=Z:\BusBuddy-3\"

echo.
echo BusBuddy — sync, rebuild, launch
echo.

if defined SHARE_ROOT (
  echo Syncing %SHARE_ROOT% -^> %LOCAL_ROOT%
  if not exist "%LOCAL_ROOT%" mkdir "%LOCAL_ROOT%"
  robocopy "%SHARE_ROOT%." "%LOCAL_ROOT%" /MIR /XD bin obj .git node_modules rag TestResults "Documentation\Archive" /XF *.user /NFL /NDL /NJH /NJS /nc /ns /np
  if errorlevel 8 (
    echo ERROR: robocopy failed.
    pause
    exit /b 1
  )
) else (
  echo WARNING: No Z:\ share with BusBuddy.sln — using %LOCAL_ROOT% only.
)

if not exist "%LOCAL_ROOT%\BusBuddy.WPF\BusBuddy.WPF.csproj" (
  echo ERROR: Project not found at %LOCAL_ROOT%
  pause
  exit /b 1
)

set "KEYFILE=%LOCAL_ROOT%\keys\SYNCFUSION_LICENSE_KEY.txt"
if exist "%KEYFILE%" (
  for /f "usebackq delims=" %%A in ("%KEYFILE%") do set "SYNCFUSION_LICENSE_KEY=%%A"
)

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: dotnet not found. winget install Microsoft.DotNet.SDK.9
  pause
  exit /b 1
)

cd /d "%LOCAL_ROOT%"
echo Restoring...
dotnet restore BusBuddy.sln -p:EnableWindowsTargeting=true --verbosity minimal
if errorlevel 1 ( echo Restore failed. & pause & exit /b 1 )

echo Building...
dotnet build BusBuddy.WPF\BusBuddy.WPF.csproj -c Debug -p:EnableWindowsTargeting=true --no-restore
if errorlevel 1 ( echo Build failed. & pause & exit /b 1 )

set "EXE=%LOCAL_ROOT%\BusBuddy.WPF\bin\Debug\net9.0-windows\BusBuddy.WPF.exe"
if exist "%EXE%" (
  echo Launching...
  cd /d "%LOCAL_ROOT%\BusBuddy.WPF\bin\Debug\net9.0-windows"
  start "" "%EXE%"
  exit /b 0
)

start "" dotnet run --project "BusBuddy.WPF\BusBuddy.WPF.csproj" -c Debug --no-build
exit /b 0
