<#
.SYNOPSIS
    Checks and installs prerequisites for OpenClawNet.
.DESCRIPTION
    Idempotent script that verifies .NET 10, Docker, Aspire CLI,
    and Ollama are installed. Optionally installs missing dependencies.
    If local Ollama is found, sets Aspire:SkipOllama to avoid a duplicate
    Docker container.
.PARAMETER SkipInstall
    Only check prerequisites, don't install anything.
.EXAMPLE
    .\setup-prerequisites.ps1
    .\setup-prerequisites.ps1 -SkipInstall
#>
param(
    [switch]$SkipInstall
)

$ErrorActionPreference = "Continue"

# --- Helpers ---

function Write-Status {
    param([string]$Icon, [string]$Label, [string]$Detail)
    $pad = 22 - $Label.Length
    if ($pad -lt 1) { $pad = 1 }
    Write-Host "  $Icon $Label$(' ' * $pad)$Detail"
}

function Test-Command {
    param([string]$Command)
    try {
        $null = Get-Command $Command -ErrorAction Stop
        return $true
    } catch {
        return $false
    }
}

function Get-OSPlatform {
    if ($IsWindows -or ($env:OS -eq "Windows_NT")) { return "Windows" }
    if ($IsMacOS) { return "macOS" }
    if ($IsLinux) { return "Linux" }
    return "Unknown"
}

# --- State ---

$platform = Get-OSPlatform
$criticalMet = $true
$requiredModels = @("gemma4:e2b")
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $repoRoot) { $repoRoot = (Get-Location).Path }

# --- Banner ---

Write-Host ""
Write-Host "  ============================================"
Write-Host "   OpenClawNet -- Prerequisites Check"
Write-Host "  ============================================"
Write-Host "   Platform: $platform"
Write-Host ""

# =============================================================================
# 1. .NET 10 SDK
# =============================================================================

$dotnetOk = $false
$dotnetVersion = ""

if (Test-Command "dotnet") {
    $dotnetVersion = (& dotnet --version 2>&1).ToString().Trim()
    if ($dotnetVersion -match "^10\.") {
        $dotnetOk = $true
        Write-Status "[OK]" ".NET 10 SDK" $dotnetVersion
    } else {
        Write-Status "[WARN]" ".NET SDK" "$dotnetVersion (need 10.x)"
    }
}

if (-not $dotnetOk) {
    if ($SkipInstall) {
        Write-Status "[FAIL]" ".NET 10 SDK" "not found (skipping install)"
        $criticalMet = $false
    } else {
        Write-Status "[INSTALL]" "Installing .NET 10 SDK..." ""
        try {
            switch ($platform) {
                "Windows" {
                    if (Test-Command "winget") {
                        & winget install Microsoft.DotNet.SDK.10 --accept-source-agreements --accept-package-agreements --silent 2>&1 | Out-Null
                    } else {
                        Write-Status "[WARN]" ".NET 10 SDK" "winget not available -- install manually from https://dot.net"
                        $criticalMet = $false
                    }
                }
                "macOS" {
                    if (Test-Command "brew") {
                        & brew install --cask dotnet-sdk 2>&1 | Out-Null
                    } else {
                        Write-Status "[WARN]" ".NET 10 SDK" "brew not available -- install manually from https://dot.net"
                        $criticalMet = $false
                    }
                }
                "Linux" {
                    # Use Microsoft's install script
                    $installScript = Join-Path $repoRoot "dotnet-install.sh"
                    try {
                        & curl -sSL https://dot.net/v1/dotnet-install.sh -o $installScript 2>&1 | Out-Null
                        & bash $installScript --channel 10.0 2>&1 | Out-Null
                        if (Test-Path $installScript) { Remove-Item $installScript -Force }
                    } catch {
                        Write-Status "[WARN]" ".NET 10 SDK" "install failed -- install manually from https://dot.net"
                        $criticalMet = $false
                    }
                }
            }
            # Re-check
            if (Test-Command "dotnet") {
                $dotnetVersion = (& dotnet --version 2>&1).ToString().Trim()
                if ($dotnetVersion -match "^10\.") {
                    $dotnetOk = $true
                    Write-Status "[OK]" ".NET 10 SDK" "$dotnetVersion (just installed)"
                }
            }
            if (-not $dotnetOk) {
                Write-Status "[FAIL]" ".NET 10 SDK" "installation did not complete"
                $criticalMet = $false
            }
        } catch {
            Write-Status "[FAIL]" ".NET 10 SDK" "install failed: $_"
            $criticalMet = $false
        }
    }
}

# =============================================================================
# 2. Docker Desktop
# =============================================================================

$dockerOk = $false
$dockerVersion = ""

if (Test-Command "docker") {
    try {
        $dockerVersion = (& docker --version 2>&1).ToString().Trim()
        if ($dockerVersion -match "(\d+\.\d+\.\d+)") {
            $dockerVersion = $Matches[1]
        }
        $dockerOk = $true
        Write-Status "[OK]" "Docker Desktop" $dockerVersion
    } catch {
        # docker command exists but daemon might not be running
        Write-Status "[WARN]" "Docker" "command found but daemon may not be running"
    }
}

if (-not $dockerOk) {
    if ($SkipInstall) {
        Write-Status "[FAIL]" "Docker Desktop" "not found (skipping install)"
        $criticalMet = $false
    } else {
        Write-Status "[INSTALL]" "Installing Docker..." ""
        try {
            switch ($platform) {
                "Windows" {
                    if (Test-Command "winget") {
                        & winget install Docker.DockerDesktop --accept-source-agreements --accept-package-agreements --silent 2>&1 | Out-Null
                        Write-Status "[OK]" "Docker Desktop" "installed (restart may be required)"
                        $dockerOk = $true
                    } else {
                        Write-Status "[WARN]" "Docker Desktop" "winget not available -- install from https://docker.com/products/docker-desktop"
                        $criticalMet = $false
                    }
                }
                "macOS" {
                    if (Test-Command "brew") {
                        & brew install --cask docker 2>&1 | Out-Null
                        Write-Status "[OK]" "Docker Desktop" "installed (launch Docker.app to start)"
                        $dockerOk = $true
                    } else {
                        Write-Status "[WARN]" "Docker Desktop" "brew not available -- install from https://docker.com/products/docker-desktop"
                        $criticalMet = $false
                    }
                }
                "Linux" {
                    if (Test-Command "apt-get") {
                        & curl -fsSL https://get.docker.com | bash 2>&1 | Out-Null
                        Write-Status "[OK]" "Docker" "installed via get.docker.com"
                        $dockerOk = $true
                    } else {
                        Write-Status "[WARN]" "Docker" "install manually -- see https://docs.docker.com/engine/install/"
                        $criticalMet = $false
                    }
                }
            }
        } catch {
            Write-Status "[FAIL]" "Docker" "install failed: $_"
            $criticalMet = $false
        }
    }
}

# =============================================================================
# 3. Aspire CLI (see https://aspire.dev/get-started/install-cli/)
#    The Aspire CLI replaces the old "dotnet workload install aspire" approach.
#    Install via the official install script from aspire.dev.
# =============================================================================

$aspireOk = $false

# Check for Aspire CLI first (new approach)
if (Test-Command "aspire") {
    try {
        $aspireVersion = (& aspire --version 2>&1).ToString().Trim()
        $aspireOk = $true
        Write-Status "[OK]" "Aspire CLI" $aspireVersion
    } catch {
        # aspire command exists but may have issues
    }
}

# Fallback: check legacy workload (still valid for older setups)
if (-not $aspireOk -and $dotnetOk) {
    $workloadList = & dotnet workload list 2>&1 | Out-String
    if ($workloadList -match "aspire") {
        $aspireOk = $true
        Write-Status "[OK]" "Aspire (workload)" "installed (consider migrating to Aspire CLI)"
    }
}

if (-not $aspireOk) {
    if ($SkipInstall) {
        Write-Status "[FAIL]" "Aspire CLI" "not found (skipping install)"
    } else {
        Write-Status "[INSTALL]" "Installing Aspire CLI..." ""
        try {
            # Official install method: https://aspire.dev/get-started/install-cli/
            switch ($platform) {
                "Windows" {
                    # PowerShell install script
                    $installScript = Join-Path $repoRoot "aspire-install.ps1"
                    Invoke-WebRequest -Uri "https://aspire.dev/install.ps1" -OutFile $installScript -UseBasicParsing
                    & pwsh -NoProfile -ExecutionPolicy Bypass -File $installScript 2>&1 | Out-Null
                    if (Test-Path $installScript) { Remove-Item $installScript -Force }
                }
                default {
                    # Bash install script for macOS/Linux
                    $installScript = Join-Path $repoRoot "aspire-install.sh"
                    & curl -sSL https://aspire.dev/install.sh -o $installScript 2>&1 | Out-Null
                    & bash $installScript 2>&1 | Out-Null
                    if (Test-Path $installScript) { Remove-Item $installScript -Force }
                }
            }
            # Re-check after install (may need new PATH)
            if (Test-Command "aspire") {
                $aspireVersion = (& aspire --version 2>&1).ToString().Trim()
                $aspireOk = $true
                Write-Status "[OK]" "Aspire CLI" "$aspireVersion (just installed)"
            } else {
                # Try refreshing PATH from user profile
                $aspireBin = Join-Path $env:USERPROFILE ".aspire" "bin"
                if (Test-Path $aspireBin) {
                    $env:PATH = "$aspireBin;$env:PATH"
                    if (Test-Command "aspire") {
                        $aspireVersion = (& aspire --version 2>&1).ToString().Trim()
                        $aspireOk = $true
                        Write-Status "[OK]" "Aspire CLI" "$aspireVersion (just installed -- restart terminal for PATH)"
                    }
                }
                if (-not $aspireOk) {
                    Write-Status "[WARN]" "Aspire CLI" "installed but may need a new terminal session"
                }
            }
        } catch {
            Write-Status "[FAIL]" "Aspire CLI" "install failed: $_ -- see https://aspire.dev/get-started/install-cli/"
        }
    }
}

# =============================================================================
# 4. Ollama
# =============================================================================

$ollamaOk = $false
$ollamaVersion = ""
$modelsReady = $false

if (Test-Command "ollama") {
    try {
        $ollamaVersion = (& ollama --version 2>&1).ToString().Trim()
        if ($ollamaVersion -match "(\d+\.\d+\.\d+)") {
            $ollamaVersion = $Matches[1]
        }
        $ollamaOk = $true
    } catch {
        # ollama exists but may have issues
    }
}

if (-not $ollamaOk) {
    if ($SkipInstall) {
        Write-Status "[WARN]" "Ollama" "not found (skipping install -- optional dependency)"
    } else {
        Write-Status "[INSTALL]" "Installing Ollama..." ""
        try {
            switch ($platform) {
                "Windows" {
                    if (Test-Command "winget") {
                        & winget install Ollama.Ollama --accept-source-agreements --accept-package-agreements --silent 2>&1 | Out-Null
                    } else {
                        Write-Status "[WARN]" "Ollama" "winget not available -- install from https://ollama.com"
                    }
                }
                "macOS" {
                    if (Test-Command "brew") {
                        & brew install ollama 2>&1 | Out-Null
                    } else {
                        Write-Status "[WARN]" "Ollama" "brew not available -- install from https://ollama.com"
                    }
                }
                "Linux" {
                    & curl -fsSL https://ollama.com/install.sh | bash 2>&1 | Out-Null
                }
            }
            # Re-check
            if (Test-Command "ollama") {
                $ollamaVersion = (& ollama --version 2>&1).ToString().Trim()
                if ($ollamaVersion -match "(\d+\.\d+\.\d+)") {
                    $ollamaVersion = $Matches[1]
                }
                $ollamaOk = $true
                Write-Status "[OK]" "Ollama" "$ollamaVersion (just installed)"
            } else {
                Write-Status "[WARN]" "Ollama" "install may require a new terminal session"
            }
        } catch {
            Write-Status "[WARN]" "Ollama" "install failed: $_ -- install manually from https://ollama.com"
        }
    }
}

# --- Pull models if Ollama is available ---

if ($ollamaOk) {
    $modelList = ""
    try {
        $modelList = (& ollama list 2>&1) | Out-String
    } catch {
        # Ollama may not be serving yet
    }

    $allModelsPresent = $true
    $readyModels = @()

    foreach ($model in $requiredModels) {
        $modelBase = $model -replace ":.*$", ""
        if ($modelList -match [regex]::Escape($modelBase)) {
            $readyModels += $model
        } else {
            $allModelsPresent = $false
            if (-not $SkipInstall) {
                Write-Status "[INSTALL]" "Pulling $model..." ""
                try {
                    & ollama pull $model 2>&1 | Out-Null
                    $readyModels += $model
                    Write-Host "     done"
                } catch {
                    Write-Status "[WARN]" "Pull $model" "failed -- pull manually with: ollama pull $model"
                }
            } else {
                Write-Status "[WARN]" "Model $model" "not found (skipping pull)"
            }
        }
    }

    $modelsReady = ($readyModels.Count -eq $requiredModels.Count)
    $modelSummary = ($readyModels -join ", ")
    if ($modelsReady) {
        Write-Status "[OK]" "Ollama" "$ollamaVersion ($modelSummary ready)"
    } else {
        Write-Status "[WARN]" "Ollama" "$ollamaVersion (some models missing)"
    }
} else {
    Write-Status "[WARN]" "Ollama" "not available -- Aspire will use a Docker container instead"
}

# =============================================================================
# 5. Set Aspire:SkipOllama if local Ollama has models
# =============================================================================

$envFile = Join-Path $repoRoot ".env"

if ($ollamaOk -and $modelsReady) {
    Write-Host ""
    Write-Host "  [OK] Local Ollama detected with models. Set Aspire:SkipOllama=true to skip the Docker container."

    # Create or update .env file
    $envKey = "Aspire__SkipOllama"
    $envLine = "$envKey=true"

    if (Test-Path $envFile) {
        $envContent = Get-Content $envFile -Raw
        if ($envContent -match $envKey) {
            # Update existing line
            $envContent = $envContent -replace "${envKey}=.*", $envLine
            Set-Content -Path $envFile -Value $envContent.TrimEnd() -NoNewline
        } else {
            # Append
            Add-Content -Path $envFile -Value "`n$envLine"
        }
    } else {
        Set-Content -Path $envFile -Value $envLine -NoNewline
    }
}

# =============================================================================
# Summary
# =============================================================================

Write-Host ""
if ($criticalMet) {
    Write-Host "  ============================================"
    Write-Host "   All prerequisites ready!"
    Write-Host "   Run: aspire start src\OpenClawNet.AppHost"
    Write-Host "  ============================================"
    Write-Host ""
    exit 0
} else {
    Write-Host "  ============================================"
    Write-Host "   [WARN]  Some critical prerequisites are missing."
    Write-Host "   .NET 10 SDK and Docker are required."
    Write-Host "   Fix the issues above and re-run this script."
    Write-Host "  ============================================"
    Write-Host ""
    exit 1
}
