# Demo 2 — Introduce Bugs for Error Detection Demo
# This script injects 3 intentional bugs into the main OpenClawNet solution.
# Run restore-original.ps1 to undo all changes.

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..\..')
$backupDir = Join-Path $PSScriptRoot 'original-backups'

Write-Host "🐛 Demo 2: Injecting bugs into OpenClawNet..." -ForegroundColor Yellow
Write-Host "   Repo root: $repoRoot"
Write-Host ""

# Create backup directory
if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
}

# --- Bug 1: Wrong Ollama endpoint (port 11434 → 11435) ---
$appSettings = Join-Path $repoRoot 'src\OpenClawNet.Gateway\appsettings.json'
Write-Host "[Bug 1] Wrong Ollama endpoint..." -ForegroundColor Red
Copy-Item $appSettings (Join-Path $backupDir 'appsettings.json') -Force
$content = Get-Content $appSettings -Raw
$content = $content -replace 'localhost:11434', 'localhost:11435'
Set-Content $appSettings $content -NoNewline
Write-Host "   ✅ Changed Ollama port 11434 → 11435 in Gateway/appsettings.json"

# --- Bug 2: Missing OllamaAgentProvider DI registration ---
$programCs = Join-Path $repoRoot 'src\OpenClawNet.Gateway\Program.cs'
Write-Host "[Bug 2] Missing DI registration..." -ForegroundColor Red
Copy-Item $programCs (Join-Path $backupDir 'Program.cs') -Force
$content = Get-Content $programCs -Raw
$content = $content -replace '(builder\.Services\.AddSingleton<OllamaAgentProvider>\(\);)', '// BUG: $1'
$content = $content -replace '(builder\.Services\.AddSingleton<IAgentProvider>\(sp => sp\.GetRequiredService<OllamaAgentProvider>\(\)\);)', '// BUG: $1'
Set-Content $programCs $content -NoNewline
Write-Host "   ✅ Commented out OllamaAgentProvider registration in Gateway/Program.cs"

# --- Bug 3: Broken NavMenu item ---
$navMenu = Join-Path $repoRoot 'src\OpenClawNet.Web\Components\Layout\NavMenu.razor'
Write-Host "[Bug 3] Broken NavMenu item..." -ForegroundColor Red
Copy-Item $navMenu (Join-Path $backupDir 'NavMenu.razor') -Force
$content = Get-Content $navMenu -Raw
$brokenItem = @'
        <div class="nav-item px-3">
            <NavLink class="nav-link" href="broken-page">
                <span class="bi bi-bug-fill" aria-hidden="true"></span> Debug Tools
            </NavLink>
        </div>
'@
$content = $content -replace '(</nav>)', "$brokenItem`n    `$1"
Set-Content $navMenu $content -NoNewline
Write-Host "   ✅ Added broken 'Debug Tools' nav item in Web/NavMenu.razor"

Write-Host ""
Write-Host "🐛 All 3 bugs injected! Start the app with 'aspire run' to see errors." -ForegroundColor Yellow
Write-Host "   Run restore-original.ps1 when done." -ForegroundColor DarkGray
