param(
    [Parameter(Mandatory=$true)]
    [ValidateRange(1,4)]
    [int]$Session,

    [string]$PlanRepo = "D:\openclawnet\openclawnet-plan",
    [string]$PublicRepo = "D:\openclawnet\openclawnet"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Preparing OpenClawNet public repo for Session $Session ===" -ForegroundColor Cyan

# Define which projects belong to each session (cumulative)
$SessionProjects = @{
    1 = @(
        "OpenClawNet.Models.Abstractions",
        "OpenClawNet.Models.Ollama",
        "OpenClawNet.Storage",
        "OpenClawNet.ServiceDefaults",
        "OpenClawNet.Gateway",
        "OpenClawNet.Web",
        "OpenClawNet.AppHost"
    )
    2 = @(
        "OpenClawNet.Tools.Abstractions",
        "OpenClawNet.Tools.Core",
        "OpenClawNet.Tools.FileSystem",
        "OpenClawNet.Tools.Shell",
        "OpenClawNet.Tools.Web",
        "OpenClawNet.Tools.Scheduler",
        "OpenClawNet.Agent",
        "OpenClawNet.Skills",
        "OpenClawNet.Memory"
    )
    3 = @()  # No new projects — just Gateway overlay
    4 = @(
        "OpenClawNet.Models.AzureOpenAI",
        "OpenClawNet.Models.Foundry"
    )
}

# Copy source projects for this session and all previous
for ($s = 1; $s -le $Session; $s++) {
    foreach ($proj in $SessionProjects[$s]) {
        $src = Join-Path $PlanRepo "src\$proj"
        $dst = Join-Path $PublicRepo "src\$proj"
        if (Test-Path $src) {
            if (-not (Test-Path $dst)) {
                Write-Host "  Copying project: $proj" -ForegroundColor Green
                Copy-Item -Path $src -Destination $dst -Recurse -Exclude @("bin", "obj")
            }
        } else {
            Write-Host "  WARNING: Source not found: $src" -ForegroundColor Yellow
        }
    }
}

# Copy test projects for session 4
if ($Session -ge 4) {
    $testProjects = @("OpenClawNet.UnitTests", "OpenClawNet.IntegrationTests")
    foreach ($tp in $testProjects) {
        $src = Join-Path $PlanRepo "tests\$tp"
        $dst = Join-Path $PublicRepo "tests\$tp"
        if ((Test-Path $src) -and -not (Test-Path $dst)) {
            Write-Host "  Copying test project: $tp" -ForegroundColor Green
            New-Item -ItemType Directory -Path (Split-Path $dst) -Force | Out-Null
            Copy-Item -Path $src -Destination $dst -Recurse -Exclude @("bin", "obj")
        }
    }
}

# Copy solution file for this session
$slnxSrc = Join-Path $PlanRepo "scripts\stages\session-$Session\OpenClawNet.slnx"
$slnxDst = Join-Path $PublicRepo "OpenClawNet.slnx"
if (Test-Path $slnxSrc) {
    Write-Host "  Updating solution file for Session $Session" -ForegroundColor Yellow
    Copy-Item -Path $slnxSrc -Destination $slnxDst -Force
} else {
    Write-Host "  WARNING: Solution file not found: $slnxSrc" -ForegroundColor Yellow
}

# Copy Gateway overlay files for this session
$overlayBase = Join-Path $PlanRepo "scripts\stages\session-$Session\src"
if (Test-Path $overlayBase) {
    Write-Host "  Applying Gateway overlay for Session $Session" -ForegroundColor Yellow
    Get-ChildItem -Path $overlayBase -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Substring($overlayBase.Length + 1)
        $dstFile = Join-Path $PublicRepo "src\$relativePath"
        $dstDir = Split-Path $dstFile
        if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
        Copy-Item -Path $_.FullName -Destination $dstFile -Force
        Write-Host "    Overlay: src\$relativePath" -ForegroundColor DarkYellow
    }
}

# Copy session materials directory
$sessionDir = Join-Path $PublicRepo "sessions\session-$Session"
if (-not (Test-Path $sessionDir)) { New-Item -ItemType Directory -Path $sessionDir -Force | Out-Null }

# Copy session materials (README, speaker-script, copilot-prompts, slides)
$materialsSrc = Join-Path $PlanRepo "docs\sessions\session-$Session"
if (Test-Path $materialsSrc) {
    Write-Host "  Copying session $Session materials" -ForegroundColor Green
    Get-ChildItem $materialsSrc -File | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $sessionDir $_.Name) -Force
        Write-Host "    $($_.Name)" -ForegroundColor DarkGreen
    }
}

# Copy built slides HTML
$slidesHtml = Join-Path $PlanRepo "docs\presentations\session-$Session\slides.html"
if (Test-Path $slidesHtml) {
    Copy-Item $slidesHtml (Join-Path $sessionDir "slides.html") -Force
    Write-Host "  Copied slides.html" -ForegroundColor Green
}

# Copy skills sample files for session 3+
if ($Session -ge 3) {
    $skillsSrc = Join-Path $PlanRepo "skills"
    $skillsDst = Join-Path $PublicRepo "skills"
    if (Test-Path $skillsSrc) {
        $samplesSrc = Join-Path $skillsSrc "samples"
        $samplesDst = Join-Path $skillsDst "samples"
        if ((Test-Path $samplesSrc) -and -not (Test-Path $samplesDst)) {
            Write-Host "  Copying skill samples" -ForegroundColor Green
            New-Item -ItemType Directory -Path $skillsDst -Force | Out-Null
            Copy-Item -Path $samplesSrc -Destination $samplesDst -Recurse -Force
        }
    }
}

# Build and verify
Write-Host "`n=== Building solution ===" -ForegroundColor Cyan
Push-Location $PublicRepo
try {
    dotnet build --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Build succeeded!" -ForegroundColor Green

    if ($Session -ge 4) {
        Write-Host "`n=== Running tests ===" -ForegroundColor Cyan
        dotnet test --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "TESTS FAILED!" -ForegroundColor Red
            exit 1
        }
        Write-Host "All tests passed!" -ForegroundColor Green
    }
} finally {
    Pop-Location
}

Write-Host "`n=== Session $Session preparation complete! ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Projects in solution:" -ForegroundColor White
$slnxContent = Get-Content $slnxDst
$slnxContent | Select-String 'Path="' | ForEach-Object {
    $match = [regex]::Match($_.Line, 'Path="([^"]+)"')
    if ($match.Success) {
        Write-Host "  - $($match.Groups[1].Value)" -ForegroundColor Gray
    }
}
