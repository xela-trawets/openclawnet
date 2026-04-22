# embed-assets.ps1
# Encodes generated PNG images as base64 data URIs and injects them
# into docs/presentations/shared/embedded-assets.json so that build.js can
# embed them inline in slides.
#
# Keys are: "<category>/<basename>" e.g. "slide-illustrations/arch-layers"
# Use in slides.md with: ![alt](asset://slide-illustrations/arch-layers)
#
# Usage:
#   .\embed-assets.ps1                          # Embed all categories
#   .\embed-assets.ps1 -Category slides         # Only docs/design/assets/slides/
#   .\embed-assets.ps1 -Category slide-illustrations
#   .\embed-assets.ps1 -Filter "arch-layers","streaming-flow"  # Specific files
#
# After running, rebuild slides:
#   cd ..\..\docs\presentations && node build.js

param(
    [string]$Category  = "",
    [string[]]$Filter  = @()
)

$ErrorActionPreference = "Stop"
$scriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot     = Resolve-Path (Join-Path $scriptDir ".." "..")
$allAssetsDir = Join-Path $repoRoot "docs" "design" "assets"
$embeddedPath = Join-Path $repoRoot "docs" "presentations" "shared" "embedded-assets.json"

if (-not (Test-Path $embeddedPath)) {
    Write-Error "embedded-assets.json not found: $embeddedPath"
    exit 1
}

# Determine which category folders to process
if ($Category -ne "") {
    $categoryDirs = @(Join-Path $allAssetsDir $Category)
} else {
    $categoryDirs = Get-ChildItem -Path $allAssetsDir -Directory | Select-Object -ExpandProperty FullName
}

# Load existing assets JSON
$assets = Get-Content $embeddedPath -Raw | ConvertFrom-Json -AsHashtable

$totalUpdated = 0

foreach ($dir in $categoryDirs) {
    if (-not (Test-Path $dir)) {
        Write-Warning "Category directory not found: $dir"
        continue
    }

    $catName = Split-Path $dir -Leaf
    $pngFiles = Get-ChildItem -Path $dir -Filter "*.png" | Sort-Object Name

    if ($Filter.Count -gt 0) {
        $pngFiles = $pngFiles | Where-Object { $Filter -contains $_.BaseName }
    }

    if ($pngFiles.Count -eq 0) { continue }

    Write-Host "📁 $catName/"
    foreach ($file in $pngFiles) {
        $key    = "$catName/$($file.BaseName)"
        $bytes  = [System.IO.File]::ReadAllBytes($file.FullName)
        $base64 = [Convert]::ToBase64String($bytes)
        $dataUri = "data:image/png;base64,$base64"

        $action     = if ($assets.ContainsKey($key)) { "updated" } else { "added" }
        $assets[$key] = $dataUri
        $fileSizeKb = [math]::Round($file.Length  / 1KB, 1)
        $b64SizeKb  = [math]::Round($dataUri.Length / 1KB, 1)
        Write-Host "  ✅ $key  (${fileSizeKb}KB → ${b64SizeKb}KB base64) [$action]"
        $totalUpdated++
    }
}

if ($totalUpdated -eq 0) {
    Write-Host "  ⚠️  No PNG files found. Check -Category or -Filter args."
    exit 0
}

# Write back as formatted JSON (preserves existing keys: dotnetLogo, ocnIcon, etc.)
$assets | ConvertTo-Json -Depth 3 | Set-Content $embeddedPath -Encoding UTF8

Write-Host ""
Write-Host "✅ Embedded $totalUpdated image(s) into embedded-assets.json"
Write-Host ""
Write-Host "Use in slides.md:"
Write-Host "  ![Description](asset://category/basename)"
Write-Host ""
Write-Host "Next step: cd docs\presentations && node build.js"
