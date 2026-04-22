# OpenClawNet Image Generator — Quick Launch Script
# Supports MAI-Image-2 (default) and FLUX.2 Pro via Microsoft Foundry
#
# Setup (one-time, stored securely per-user):
#   cd scripts\ImageGenerator
#   dotnet user-secrets set Endpoint  "https://your-resource.services.ai.azure.com"
#   dotnet user-secrets set ApiKey    "your-api-key"
#   dotnet user-secrets set Model     "mai"          # mai (default) | flux2
#   dotnet user-secrets set ModelId   "MAI-Image-2"  # optional, overrides default
#
# After generating slide backgrounds, embed them into the presentations:
#   .\embed-slide-images.ps1
#   cd ..\..\docs\presentations && node build.js
#
# Usage:
#   .\generate.ps1                      # List all available prompts
#   .\generate.ps1 all                  # Generate ALL images
#   .\generate.ps1 1A 2A 3B            # Generate specific IDs
#   .\generate.ps1 category:slides     # Generate all slide backgrounds
#   .\generate.ps1 --dry-run all       # Preview what would be generated

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

Push-Location $scriptDir
try {
    if ($Args.Count -gt 0) {
        dotnet run -- @Args
    } else {
        dotnet run
    }
} finally {
    Pop-Location
}
