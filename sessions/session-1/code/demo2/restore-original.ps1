# Demo 2 — Restore Original Files
# Undoes the bugs injected by introduce-bugs.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..\..')
$backupDir = Join-Path $PSScriptRoot 'original-backups'

Write-Host "🔧 Demo 2: Restoring original files..." -ForegroundColor Green

if (-not (Test-Path $backupDir)) {
    Write-Host "❌ No backups found. Run introduce-bugs.ps1 first." -ForegroundColor Red
    exit 1
}

$files = @(
    @{ Backup = 'appsettings.json'; Target = 'src\OpenClawNet.Gateway\appsettings.json' },
    @{ Backup = 'Program.cs';       Target = 'src\OpenClawNet.Gateway\Program.cs' },
    @{ Backup = 'NavMenu.razor';    Target = 'src\OpenClawNet.Web\Components\Layout\NavMenu.razor' }
)

foreach ($file in $files) {
    $backup = Join-Path $backupDir $file.Backup
    $target = Join-Path $repoRoot $file.Target
    if (Test-Path $backup) {
        Copy-Item $backup $target -Force
        Write-Host "   ✅ Restored $($file.Target)"
    } else {
        Write-Host "   ⚠️  Backup not found: $($file.Backup)" -ForegroundColor Yellow
    }
}

# Clean up backup directory
Remove-Item $backupDir -Recurse -Force
Write-Host ""
Write-Host "🔧 All files restored to original state." -ForegroundColor Green
