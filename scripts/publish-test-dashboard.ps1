<#
.SYNOPSIS
  Runs Playwright E2E tests and unit tests, then publishes the HTML report 
  to the public repo (elbruno/openclawnet) so GitHub Pages can serve it.

.PARAMETER PublicRepoPath
  Local path to the public repo clone. Defaults to C:\src\openclawnet.

.PARAMETER SkipTests
  When set, skips running the tests and only copies/publishes the last
  generated report.

.PARAMETER Headed
  When set, runs Playwright tests in headed mode (browser visible).

.EXAMPLE
  .\publish-test-dashboard.ps1
  .\publish-test-dashboard.ps1 -SkipTests
  .\publish-test-dashboard.ps1 -Headed
  .\publish-test-dashboard.ps1 -PublicRepoPath "D:\repos\openclawnet"
#>
[CmdletBinding()]
param(
    [string]$PublicRepoPath = "C:\src\openclawnet",
    [switch]$SkipTests,
    [switch]$Headed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent

# ── Run tests ────────────────────────────────────────────────────────
if (-not $SkipTests) {
    Write-Host "`n🧪 Running Playwright E2E tests..." -ForegroundColor Cyan

    $env:NUGET_PACKAGES = "$env:USERPROFILE\.nuget\packages2"
    $env:Aspire__SkipOllama = "true"

    if ($Headed) {
        $env:PLAYWRIGHT_HEADED = "true"
        Write-Host "🖥️  Running in headed mode (browser visible)" -ForegroundColor Yellow
    } else {
        $env:PLAYWRIGHT_HEADED = $null
    }

    $resultsDir = Join-Path $repoRoot "TestResults"
    if (Test-Path $resultsDir) { Remove-Item $resultsDir -Recurse -Force }

    $testProject = Join-Path $repoRoot "tests\OpenClawNet.PlaywrightTests"
    dotnet test $testProject `
        --logger "html;LogFileName=test-results.html" `
        --logger "trx;LogFileName=test-results.trx" `
        --results-directory $resultsDir

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Tests exited with code $LASTEXITCODE — report will still be published."
    }

    # Run unit tests
    Write-Host "`n🧪 Running unit tests..." -ForegroundColor Cyan
    $unitTestProject = Join-Path $repoRoot "tests\OpenClawNet.UnitTests"
    dotnet test $unitTestProject `
        --filter "Category!=Live" `
        --logger "trx;LogFileName=unit-test-results.trx" `
        --results-directory $resultsDir

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Unit tests exited with code $LASTEXITCODE — report will still be published."
    }

    # Run integration tests
    Write-Host "`n🧪 Running integration tests..." -ForegroundColor Cyan
    $integrationTestProject = Join-Path $repoRoot "tests\OpenClawNet.IntegrationTests"
    dotnet test $integrationTestProject `
        --logger "trx;LogFileName=integration-test-results.trx" `
        --results-directory $resultsDir

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Integration tests exited with code $LASTEXITCODE — report will still be published."
    }
}

# ── Locate report ────────────────────────────────────────────────────
$resultsDir = Join-Path $repoRoot "TestResults"
$htmlReport = Join-Path $resultsDir "test-results.html"

if (-not (Test-Path $htmlReport)) {
    Write-Error "No HTML report found at $htmlReport. Run tests first (omit -SkipTests)."
    exit 1
}

# ── Copy to public repo ─────────────────────────────────────────────
$dashboardDir = Join-Path $PublicRepoPath "docs\test-dashboard"
if (-not (Test-Path $PublicRepoPath)) {
    Write-Error "Public repo not found at $PublicRepoPath. Clone elbruno/openclawnet there first."
    exit 1
}

Write-Host "`n📁 Copying report to $dashboardDir ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $dashboardDir -Force | Out-Null

# Copy TRX results (the branded index.html is maintained separately — do NOT overwrite it)
$trxReport = Join-Path $resultsDir "test-results.trx"
if (Test-Path $trxReport) {
    Copy-Item $trxReport (Join-Path $dashboardDir "test-results.trx") -Force
}

# Copy unit test TRX
$unitTrx = Join-Path $resultsDir "unit-test-results.trx"
if (Test-Path $unitTrx) {
    Copy-Item $unitTrx (Join-Path $dashboardDir "unit-test-results.trx") -Force
}

# Copy integration test TRX
$integrationTrx = Join-Path $resultsDir "integration-test-results.trx"
if (Test-Path $integrationTrx) {
    Copy-Item $integrationTrx (Join-Path $dashboardDir "integration-test-results.trx") -Force
}

# Copy raw HTML report as a secondary file (not index.html) for reference
Copy-Item $htmlReport (Join-Path $dashboardDir "dotnet-report.html") -Force

# Update dashboard generation date
$dashboardIndex = Join-Path $dashboardDir "index.html"
if (Test-Path $dashboardIndex) {
    $dateStr = Get-Date -Format "MMMM d, yyyy 'at' h:mm tt 'UTC'"
    $html = Get-Content $dashboardIndex -Raw
    $html = $html -replace '<!-- DASHBOARD_DATE -->.*?<!-- /DASHBOARD_DATE -->', "<!-- DASHBOARD_DATE -->$dateStr<!-- /DASHBOARD_DATE -->"
    Set-Content $dashboardIndex $html -NoNewline
}

# ── Commit & push────────────────────────────────────────────────────
Write-Host "`n🚀 Committing to public repo..." -ForegroundColor Cyan
Push-Location $PublicRepoPath
try {
    git add docs/test-dashboard/
    $status = git status --porcelain docs/test-dashboard/
    if ($status) {
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm"
        git commit -m "chore: update E2E test dashboard ($timestamp)"
        git push
        Write-Host "`n✅ Dashboard published. GitHub Pages will deploy automatically." -ForegroundColor Green
    } else {
        Write-Host "`nℹ️  No changes to publish — dashboard is already up to date." -ForegroundColor Yellow
    }
} finally {
    Pop-Location
}
