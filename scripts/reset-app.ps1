#!/usr/bin/env pwsh
# Resets the OpenClaw .NET app to a fresh state (dev mode only)
# Usage: ./scripts/reset-app.ps1 [-Port 5010]

param(
    [int]$Port = 5010
)

$uri = "http://localhost:$Port/api/dev/reset"
Write-Host "🔄 Resetting app at $uri..." -ForegroundColor Yellow

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -ContentType "application/json"
    Write-Host "✅ $($response.message)" -ForegroundColor Green
    if ($response.deleted) {
        $response.deleted.PSObject.Properties | ForEach-Object {
            Write-Host "   $($_.Name): $($_.Value) rows deleted" -ForegroundColor Cyan
        }
    }
} catch {
    Write-Host "❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Make sure the app is running in Development mode on port $Port" -ForegroundColor Gray
}
