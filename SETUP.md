# 🚀 OpenClaw .NET — Setup Guide

A complete, public‑facing setup guide for running **OpenClaw .NET** locally — the .NET 10 + Aspire AI agent platform from the [Microsoft Reactor series](https://developer.microsoft.com/en-us/reactor/series/s-1652/).

> **TL;DR** — Install the .NET 10 SDK + Aspire CLI, install [Ollama](https://ollama.ai), `ollama pull llama3.2`, clone, build, then `aspire start src\OpenClawNet.AppHost`. Open the Web URL from the Aspire dashboard and chat.

For a deeper walkthrough see [`docs/manuals/01-local-installation.md`](docs/manuals/01-local-installation.md). For the full prerequisites reference see [`docs/manuals/00-prerequisites.md`](docs/manuals/00-prerequisites.md).

---

## 1. Prerequisites

| Tool | Required? | Notes / Install |
|------|-----------|-----------------|
| **.NET 10 SDK** | ✅ Required | [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0) — verify with `dotnet --version` (expect `10.0.x` or higher). |
| **.NET Aspire CLI** | ✅ Required | `dotnet workload install aspire` then `aspire --version`. Docs: [aspire setup tooling](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling). |
| **Docker Desktop** | ✅ Required | Used by Aspire to spin up Ollama + sqlite-web containers. Make sure it is **running** before you launch the AppHost. |
| **Git** | ✅ Required | [git-scm.com](https://git-scm.com/downloads) |
| **VS Code** *or* **Visual Studio 2026** | ✅ Required | Either works. VS Code: install the C# Dev Kit + GitHub Copilot extensions. |
| **A model provider — pick at least one** | ✅ Required | One of the four below. |
| ↳ **Ollama** *(default, recommended)* | one‑of | [ollama.ai](https://ollama.ai). Default endpoint `http://localhost:11434`. |
| ↳ **Foundry Local** | one‑of | [Foundry Local](https://devblogs.microsoft.com/foundry/foundry-local-ga/) — fully offline Microsoft runtime. |
| ↳ **Azure OpenAI** | one‑of | Requires endpoint, deployment name and API key (or managed identity). |
| ↳ **GitHub Copilot** | one‑of | Personal Access Token with Copilot access. |

> **Tip:** You can change the active provider at runtime from the **Settings** page in the Web UI — see [`docs/manuals/10-settings.md`](docs/manuals/10-settings.md).

---

## 2. Hardware Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| **RAM** | 16 GB | 32 GB (for comfortable local LLM inference + Aspire + browser) |
| **CPU** | 4 cores | 8+ cores |
| **Disk** | ~10 GB (code + small models like `llama3.2:3b`, `gemma3:4b`) | +20 GB if you plan to pull large models (Qwen‑TTS, mid‑size vision models) |
| **GPU** | Not required (CPU inference works) | NVIDIA GPU with CUDA (or Apple Silicon) for fast local inference |
| **OS** | Windows 10/11, macOS 13+, or modern Linux | — |

> **Note:** Each Ollama model is typically 2–8 GB; multimodal / TTS models can be 10 GB+. Plan disk accordingly.

---

## 3. Clone

```bash
git clone https://github.com/elbruno/openclawnet.git
cd openclawnet
```

---

## 4. Pull a Model (Ollama path)

The default provider is Ollama. Pull at least one chat model and one tool‑capable model:

```bash
# General chat
ollama pull llama3.2

# Tool/function-calling capable (used by the Tools demos in Session 2+)
ollama pull gemma3:4b
```

Verify:

```bash
ollama list
```

> Using **Foundry Local** instead? Run `foundrylocal model download --assigned phi-4` and set the provider to `foundrylocal` in the Settings UI.

---

## 5. Build

Use the dedicated NuGet package cache to avoid clashing with other repos on your machine:

```powershell
$env:NUGET_PACKAGES="$env:USERPROFILE\.nuget\packages2"
dotnet build src\OpenClawNet.AppHost\OpenClawNet.AppHost.csproj
```

A first restore can take a few minutes. Expect `Build succeeded` with **0 errors** (warnings are fine).

To build the full solution instead:

```powershell
dotnet build OpenClawNet.slnx
```

---

## 6. Run

Launch everything with Aspire:

```powershell
aspire start src\OpenClawNet.AppHost
```

Aspire will:

1. Start the **Gateway** (HTTP control plane), **Web UI**, **Scheduler**, the **Ollama** container, the **SQLite** database, and **sqlite-web**.
2. Open the **Aspire dashboard** in your browser (typically `http://localhost:15888/login?t=<token>`).
3. Print URLs for each resource.

Open the URL next to the `web` resource — usually <http://localhost:5010> (the actual port is printed by Aspire and shown in the dashboard).

| Resource | What it does |
|----------|--------------|
| `gateway` | HTTP/REST control plane — model + tool + job APIs |
| `web` | Blazor chat UI, Settings, Tools, Jobs pages |
| `scheduler` | Cron + one‑shot job runner |
| `ollama` | Local LLM container |
| `openclawnet-db` | SQLite database (sessions, messages, jobs, memory) |
| `sqlite-web` | Browser admin UI for the SQLite DB |

---

## 7. First Chat

1. Open the **Web UI** (the URL next to the `web` resource — e.g. <http://localhost:5010>).
2. Click **Chat** in the left nav.
3. Type:

   ```
   Hello
   ```

4. You should see a streamed response token‑by‑token. 🎉

If the model is silent, jump to [Troubleshooting](#9-troubleshooting).

---

## 8. Try a Tool — Built‑in Job Templates

OpenClaw .NET ships with several **built‑in job templates** that wire together file‑system, web, and LLM tools.

1. In the Web UI, navigate to **Jobs → Templates** (`/jobs/templates`).
2. Click **Watched folder → Markdown → Summary**.
3. Customize the watched folder path and the destination summary file.
4. Click **Run**.
5. Watch the run timeline stream events under **Jobs → Runs**.

Drop a `.md` file into the watched folder and you should see the agent generate a summary into the destination file within a few seconds.

For more tool walkthroughs see:

- [`docs/manuals/20-tools.md`](docs/manuals/20-tools.md) — built‑in tools reference
- [`docs/manuals/30-jobs.md`](docs/manuals/30-jobs.md) — jobs + scheduler
- [`docs/demos/tools/`](docs/demos/tools/) — five end‑to‑end tool demos

---

## 9. Provider Configuration

The default provider is **Ollama** with model `gemma4:e2b` / `llama3.2`. Switch providers at runtime from the **Settings** page in the Web UI, or by editing `src/OpenClawNet.Gateway/appsettings.json`:

```json
{
  "Model": {
    "Provider": "ollama",
    "Model": "llama3.2",
    "Endpoint": "http://localhost:11434",
    "Temperature": 0.7,
    "MaxTokens": 4096
  }
}
```

| Provider | `Provider` value | Required settings |
|----------|------------------|-------------------|
| Ollama | `ollama` | `Endpoint`, `Model` |
| Foundry Local | `foundrylocal` | `Model` |
| Azure OpenAI | `azureopenai` | `Endpoint`, `Deployment`, `ApiKey` (or managed identity) |
| Microsoft Foundry | `foundry` | `Endpoint`, `AgentId` |
| GitHub Copilot | `githubcopilot` | `Token` (PAT with Copilot access) |

For secrets handling (Azure OpenAI keys, Copilot PATs) follow the patterns in [`docs/manuals/10-settings.md`](docs/manuals/10-settings.md). **Never** commit secrets — runtime values live in `model-settings.json` (gitignored) and `.env` files.

---

## 10. Troubleshooting

### Ollama is not running / chat replies are empty

```bash
curl http://localhost:11434/api/tags
ollama pull llama3.2
```

Make sure Docker Desktop is up; Aspire will otherwise fail to start the Ollama container. On Windows, restart Docker Desktop and wait for the whale icon to stop animating.

### Aspire DLL locks on rebuild ("file in use")

Aspire keeps the AppHost process alive even after `Ctrl+C`. If a `dotnet build` fails with locked DLLs:

```powershell
Get-Process dotnet | Stop-Process -Force
dotnet build OpenClawNet.slnx
```

### NuGet package cache issues / mysterious restore errors

This repo is built against a **dedicated package cache** to avoid colliding with other .NET projects:

```powershell
$env:NUGET_PACKAGES="$env:USERPROFILE\.nuget\packages2"
dotnet restore OpenClawNet.slnx --force
```

If a restore is still broken, clear the cache:

```powershell
Remove-Item "$env:USERPROFILE\.nuget\packages2" -Recurse -Force
dotnet restore OpenClawNet.slnx
```

### `aspire: command not found`

```bash
dotnet workload install aspire
```

### Database is locked or corrupt

Stop the AppHost first, then delete the SQLite file:

```powershell
Remove-Item src\OpenClawNet.AppHost\.data\openclawnet.db
```

Aspire recreates the schema on next launch.

### Build fails with `NETSDK1045: requires .NET 10`

Your installed SDK is older than 10.0. Upgrade from <https://dotnet.microsoft.com/download/dotnet/10.0>.

---

## 11. What's Where

Top‑level layout of the public repo:

```
src/                                    # Application source — 40+ projects
  OpenClawNet.AppHost                   # Aspire orchestrator — start here
  OpenClawNet.Gateway                   # HTTP/REST control plane
  OpenClawNet.Web                       # Blazor Web UI (chat, settings, jobs)
  OpenClawNet.Agent                     # Agent runtime + reasoning loop
  OpenClawNet.Memory                    # Conversation summarization + embeddings
  OpenClawNet.Skills                    # Markdown+YAML skills system
  OpenClawNet.Storage                   # EF Core / SQLite persistence
  OpenClawNet.ServiceDefaults           # Shared OpenTelemetry/health setup
  OpenClawNet.Models.*                  # Provider adapters (Ollama, FoundryLocal,
                                        #   AzureOpenAI, Foundry, GitHubCopilot)
  OpenClawNet.Tools.*                   # Built-in tools (FileSystem, Shell, Web,
                                        #   Browser, GitHub, Scheduler, Calculator,
                                        #   Embeddings, ImageEdit, MarkItDown,
                                        #   Text2Image, TextToSpeech, YouTube, ...)
  OpenClawNet.Mcp.*                     # MCP server + adapters (Browser, Shell,
                                        #   FileSystem, Web, Core)
  OpenClawNet.Services.*                # Long-running services (Browser, Channels,
                                        #   Memory, Scheduler, Shell)
  OpenClawNet.Adapters.Teams            # Teams channel adapter

tests/                                  # Test projects
  OpenClawNet.UnitTests                 # xUnit unit tests
  OpenClawNet.IntegrationTests          # Integration tests
  OpenClawNet.PlaywrightTests           # End-to-end Playwright tests

scripts/                                # Helper PowerShell scripts
  setup-prerequisites.ps1               # One-shot prereq installer
  reset-app.ps1                         # Wipe local DB / state
  prepare-session.ps1                   # Session preparation helper
  publish-test-dashboard.ps1            # Run + publish E2E dashboard
  ImageGenerator/                       # FLUX image generation utility

docs/
  manuals/                              # User-facing manuals (00–30)
  demos/                                # Step-by-step demo scripts
    aspire-stack/                       # Full-stack demos
    gateway-only/                       # Gateway-only demos
    real-world/                         # Real-world scenario demos (5)
    tools/                              # Tool-focused demos (5)
  analysis/                             # Architecture / design analyses
  test-dashboard/                       # Published test dashboard

sessions/                               # Reactor session attendee materials
  session-1/   session-2/   session-3/   session-4/
```

---

## Next Steps

- 📘 **Manuals:** [`docs/manuals/`](docs/manuals/) — prerequisites, installation, settings, tools, jobs.
- 🎬 **Demos:** [`docs/demos/`](docs/demos/) — short, focused walkthroughs.
- 🎓 **Sessions:** [`sessions/`](sessions/) — Reactor session attendee guides.
- 🛠️ **Source:** [`src/`](src/) — the full .NET 10 + Aspire codebase.

Welcome aboard! 🦀
