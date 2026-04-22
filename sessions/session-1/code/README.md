# Session 1 — Demo Code

This folder contains the **demo-ready code** for Session 1 of OpenClawNet. Each demo folder is self-contained and maps to a live demonstration in the session.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Ollama](https://ollama.com/) running locally with a model pulled (e.g., `ollama pull gemma4:e2b`)

## Demos

### Demo 1 — Console Chat with IAgentProvider (`demo1/`)

A minimal console app showing the `IAgentProvider` → `IChatClient` abstraction. Asks a hardcoded question using Ollama, with commented-out blocks showing how to switch to Azure OpenAI or GitHub Copilot SDK.

```bash
cd demo1/OpenClawNet.Demo1.Console
dotnet run
```

**Teaching point:** Provider-agnostic architecture — swap backends by changing a few lines, no other code changes.

### Demo 2 — Error Detection with Copilot CLI + Aspire (`demo2/`)

PowerShell scripts that inject **intentional bugs** into the running OpenClawNet solution. The presenter starts the app, shows errors in the Aspire Dashboard, and uses GitHub Copilot CLI to diagnose and fix them.

```powershell
cd demo2
.\introduce-bugs.ps1   # Inject 3 bugs
aspire run              # Start app, observe errors
.\restore-original.ps1  # Clean up after demo
```

**Teaching point:** Aspire observability + AI-assisted debugging — real errors, real diagnostics.

### Demo 3 — Agent Personality Switching (`demo3/`)

An enhanced console app (Plan B) that creates three agent personas — Captain Claw (pirate), Chef Byte (cooking), RoboChat (robot) — and asks each the same question. Demonstrates how `AgentProfile.Instructions` shapes behavior.

```bash
cd demo3/OpenClawNet.Demo3.Agents
dotnet run
```

**Teaching point:** Same model, same provider, different personalities via instructions.

> **Primary Demo 3** uses the full running app with workspace files from `demo-agents/`. This console app is the self-contained fallback.

## Archive

The `archive/` folder contains the original staged demo code (stage-1, stage-2, stage-3-final) from an earlier version of the workshop. These used the legacy `IModelClient` interface and are preserved for reference only.
