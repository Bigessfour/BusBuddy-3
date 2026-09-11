@echo off
REM One-shot District Map diagnostics smoke launcher
REM Do NOT set GCP_BILLING_PROJECT — API keys bill the key's project; that header causes HTTP 403.
set BUSBUDDY_MAP_DIAGNOSTICS=1
set DatabaseProvider=Postgres
cd /d C:\dev\BusBuddy-3\BusBuddy.WPF\bin\Debug\net9.0-windows
if not exist BusBuddy.WPF.exe (
  echo BusBuddy.WPF.exe missing — run utm_run_in_vm.ps1 first
  pause
  exit /b 1
)
start "" BusBuddy.WPF.exe
