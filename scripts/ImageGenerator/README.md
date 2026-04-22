# OpenClaw .NET Image Generator

Utility script that uses [ElBruno.Text2Image.Foundry v0.7.0](https://www.nuget.org/packages/ElBruno.Text2Image.Foundry/) to generate all OpenClaw .NET brand images from the prompt catalog at `docs/design/image-prompts.md` using **FLUX.2 Pro** on Microsoft Foundry.

Supports **image-to-image (img2img)** — select prompts use the official .NET logo as a reference image for brand-consistent generation.

## Prerequisites

- .NET 8.0 SDK
- A deployed FLUX.2 Pro model on Azure AI Foundry

## Setup

Store your FLUX.2 Pro credentials securely with .NET user-secrets (one-time):

```powershell
cd scripts\ImageGenerator
dotnet user-secrets set Endpoint "https://your-resource.services.ai.azure.com"
dotnet user-secrets set ApiKey "your-api-key"

# Optional overrides:
dotnet user-secrets set ModelId "FLUX.2-pro"     # deployment/model name
dotnet user-secrets set ModelName "FLUX.2 Pro"   # display name
```

Alternatively, environment variables also work: `FLUX2_ENDPOINT`, `FLUX2_APIKEY`, `FLUX2_MODELID`, `FLUX2_MODELNAME`.

## Usage

```powershell
# List all available prompts
.\generate.ps1

# Generate ALL images (saves to both repos)
.\generate.ps1 all

# Generate specific image(s) by ID
.\generate.ps1 1A 2A 3B

# Generate all images in a category
.\generate.ps1 category:logo
.\generate.ps1 category:slides
.\generate.ps1 category:blog
.\generate.ps1 category:social
.\generate.ps1 category:github
.\generate.ps1 category:webapp

# Preview what would be generated
.\generate.ps1 --dry-run all
```

## Output Locations

Images are saved to **both** repositories:

| Repo | Path |
|------|------|
| `openclawnet-plan` | `docs/design/assets/{category}/{filename}.png` |
| `openclawnet` | `docs/design/assets/{category}/{filename}.png` |

## Image Categories

| Category | IDs | Description |
|----------|-----|-------------|
| `logo` | 1A, 1B, 1C, 1D | Logo/mascot variants |
| `slides` | 2A–2E | Presentation slide backgrounds |
| `blog` | 3A–3F | Blog/article header images |
| `social` | 4A–4D | Social media cards |
| `github` | 5A–5C | GitHub social previews & README banner |
| `webapp` | 6A–6C | Blazor web app assets |
