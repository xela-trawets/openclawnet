# 🚀 Session 4: Automation + Cloud

![Duration](https://img.shields.io/badge/Duration-50%20min-blue)
![Level](https://img.shields.io/badge/Level-Intermediate%20.NET-purple)
![Session](https://img.shields.io/badge/Session-4%20of%204-green)
![FINALE](https://img.shields.io/badge/🎉-SERIES%20FINALE-E8590C)

## Overview

Your agent works locally. Users love it. Now three production challenges stand between you and a real platform: **cloud providers** (GPT-4o, SLAs, team-wide access), **automated scheduling** (background jobs without a user present), and **testing** (24 tests proving the architecture works).

This is the **series finale**. By the end, every piece connects: chat, tools, skills, memory, scheduling, health checks, and cloud providers — a complete AI agent platform built with .NET.

## Prerequisites

- ✅ Session 3 complete and working
- ✅ .NET 10 SDK, VS Code or Visual Studio
- ✅ Local LLM running (Ollama with `llama3.2` or Foundry Local)
- ✅ Understanding of: background services, HTTP clients, unit testing
- ☁️ **Optional:** Azure account with Azure OpenAI or Foundry access

## What You'll Learn

### ☁️ Stage 1: Cloud Providers (12 min)
- `IModelClient` polymorphism — one interface, three implementations (Local LLM, Azure OpenAI, Foundry)
- `AzureOpenAIModelClient` — Azure.AI.OpenAI SDK integration
- `FoundryModelClient` — custom HTTP client with manual JSON serialization
- Options pattern for provider configuration
- Switching providers with a single DI line change

### ⏰ Stage 2: Scheduling + Health (12 min)
- `BackgroundService` pattern for `JobSchedulerService`
- Cron-based job scheduling with EF Core persistence and audit trail
- `SchedulerTool` — the agent schedules itself
- `ServiceDefaults` — health checks (`/health`, `/alive`) + OpenTelemetry
- Aspire Dashboard integration

### ✅ Stage 3: Testing + Production (12 min)
- Test pyramid: 23 unit tests + 1 integration test
- Mocking patterns: `IModelClient`, `IDbContextFactory`, `ITool`, `ISkillLoader`
- Test suites: PromptComposerTests, ToolExecutorTests, SkillParserTests, ConversationStoreTests, ToolRegistryTests
- Production readiness checklist

### 🎉 Series Finale Closing (14 min)
- Full platform demo: chat → tools → skills → scheduling → health → Aspire
- Four-session series recap with architecture diagram
- Where to go from here

## Projects Covered

| Project | Key Responsibility |
|---------|-------------------|
| OpenClawNet.Models.AzureOpenAI | Azure OpenAI client (137 LOC) |
| OpenClawNet.Models.Foundry | Foundry client (195 LOC) |
| OpenClawNet.Models.Abstractions | IModelClient interface |
| OpenClawNet.Tools.Scheduler | SchedulerTool with cron support |
| OpenClawNet.Gateway | JobSchedulerService (BackgroundService) |
| OpenClawNet.ServiceDefaults | Health checks + OpenTelemetry (105 LOC) |
| OpenClawNet.UnitTests | 23 unit tests |
| OpenClawNet.IntegrationTests | 1 integration test |

## Session Materials

| Resource | Link |
|----------|------|
| 📖 Presenter Guide | [session-4-guide.md](../session-4-guide.md) |
| 📖 Guía (Español) | [session-4-guide-es.md](../session-4-guide-es.md) |
| 🎤 Speaker Script | [speaker-script.md](./speaker-script.md) |
| 🤖 Copilot Prompts | [copilot-prompts.md](./copilot-prompts.md) |
| 🖥️ Slides | [slides.md](./slides.md) |

## Git Checkpoints

- **Starting tag:** `session-4-start` (alias: `session-3-complete`)
- **Ending tag:** `session-4-complete`

## The Full Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    OpenClawNet Platform                  │
├──────────┬──────────┬──────────┬───────────────────────────┤
│  Web UI  │ REST API │  Aspire  │     Health Checks       │
├──────────┴──────────┴──────────┴───────────────────────────┤
│                   Agent Orchestrator                      │
│         ┌──────────────────────────────┐                  │
│         │     Prompt Composer          │                  │
│         │  (System + Skills + Memory)  │                  │
│         └──────────────────────────────┘                  │
├──────────┬──────────┬──────────┬───────────────────────────┤
│   Tools  │  Skills  │  Memory  │     Scheduler           │
│ Registry │  Loader  │  Store   │  BackgroundService      │
├──────────┴──────────┴──────────┴───────────────────────────┤
│                   Model Abstraction                       │
│         ┌────────┬──────────┬──────────┐                  │
│         │ Ollama │ Azure AI │ Foundry  │                  │
│         └────────┴──────────┴──────────┘                  │
├────────────────────────────────────────────────────────────┤
│              Storage (EF Core + SQLite)                   │
└────────────────────────────────────────────────────────────┘
```

## Series Recap

| Session | Topic | What We Built |
|---------|-------|--------------|
| **1** | Scaffolding + Local Chat | Aspire host, local LLM integration, Gateway, Chat UI |
| **2** | Tools + Agent Workflows | ITool, Registry, Executor, approval policies, tool loop |
| **3** | Skills + Memory | Markdown skills, YAML parsing, summarization, semantic search |
| **4** | Automation + Cloud | Cloud providers, job scheduling, health checks, 24 tests |
