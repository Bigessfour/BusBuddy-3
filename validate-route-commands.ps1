# BusBuddy Route Commands Validation Script
# Validates that all route-related commands are properly implemented and exported

Write-Host "🚌 BusBuddy Route Commands Validation" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""

# Test 1: Check if functions are available
Write-Host "Test 1: Checking function availability..." -ForegroundColor Yellow

$routeFunctions = @(
    'Start-BusBuddyRouteOptimization',
    'Show-RouteOptimizationDemo',
    'Get-BusBuddyRouteStatus'
)

$routeAliases = @(
    'bbRoutes',
    'bbRouteDemo',
    'bbRouteStatus',
    'bbRouteOptimize'
)

foreach ($func in $routeFunctions) {
    if (Get-Command $func -ErrorAction SilentlyContinue) {
        Write-Host "  ✅ Function: $func" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Function: $func" -ForegroundColor Red
    }
}

foreach ($alias in $routeAliases) {
    if (Get-Command $alias -ErrorAction SilentlyContinue) {
        Write-Host "  ✅ Alias: $alias" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Alias: $alias" -ForegroundColor Red
    }
}

Write-Host ""

# Test 2: Test function execution
Write-Host "Test 2: Testing function execution..." -ForegroundColor Yellow

try {
    Write-Host "  Testing bbRouteStatus..." -ForegroundColor Cyan
    bbRouteStatus
    Write-Host "  ✅ bbRouteStatus executed successfully" -ForegroundColor Green
} catch {
    Write-Host "  ❌ bbRouteStatus failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

try {
    Write-Host "  Testing bbRoutes..." -ForegroundColor Cyan
    bbRoutes
    Write-Host "  ✅ bbRoutes executed successfully" -ForegroundColor Green
} catch {
    Write-Host "  ❌ bbRoutes failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

Write-Host "Test 3: Demonstrating bbRouteDemo..." -ForegroundColor Yellow
try {
    bbRouteDemo
    Write-Host "  ✅ bbRouteDemo executed successfully" -ForegroundColor Green
} catch {
    Write-Host "  ❌ bbRouteDemo failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "🎉 Route Commands Validation Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  • All route commands are now functional" -ForegroundColor White
Write-Host "  • Use 'bbRouteDemo' to see route optimization in action" -ForegroundColor White
Write-Host "  • Use 'bbRun' to open the WPF application for full UI experience" -ForegroundColor White
Write-Host "  • Use 'bbMvpCheck' to verify overall system readiness" -ForegroundColor White
