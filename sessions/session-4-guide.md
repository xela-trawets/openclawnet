# Session 4: Automation + Cloud

**Duration:** 50 minutes | **Level:** Intermediate .NET

---

## Overview

Your agent works locally. Users love it. Now three production challenges stand between you and a real platform:

1. **Cloud providers** — Local LLMs (Ollama, Foundry Local) are great for dev, but production needs GPT-4o, SLAs, and team-wide access.
2. **Automated scheduling** — The agent should run jobs in the background without a user sitting there.
3. **Testing** — You can't ship what you can't test. 24 tests prove the architecture works.

This is the **series finale**. By the end of this session, every piece connects: chat, tools, skills, memory, scheduling, health checks, and cloud providers — a complete AI agent platform built with .NET.

---

## Before the Session

### Prerequisites

- Session 3 complete and working
- .NET 10 SDK, VS Code or Visual Studio
- Local LLM running (Ollama with `llama3.2` or Foundry Local)
- Understanding of: background services, HTTP clients, unit testing
- **Optional:** Azure account with Azure OpenAI or Foundry access

### Starting Point

- The `session-3-complete` code
- Full agent orchestration with skills and memory
- All tools implemented and working
- Database with conversation history

### Git Checkpoint

**Starting tag:** `session-4-start` (alias: `session-3-complete`)
**Ending tag:** `session-4-complete`

---

## Stage 1: Cloud Providers (12 min)

### Concepts

**Why cloud? Beyond local LLMs.**
Local LLMs like Ollama and Foundry Local are perfect for development — free, local, no credentials. But production needs more:
- **GPT-4o quality** — Better reasoning, longer context, tool calling reliability
- **SLAs** — 99.9% uptime guarantees, not "my laptop is on"
- **Team sharing** — One endpoint, many developers, centralized billing
- **Compliance** — Data residency, audit logs, enterprise security

**IModelClient polymorphism.**
The magic: one interface, three implementations. Your agent code doesn't change — only the DI registration does.

```csharp
public interface IModelClient
{
    string ProviderName { get; }
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct);
    IAsyncEnumerable<ChatResponseChunk> StreamAsync(ChatRequest request, CancellationToken ct);
    Task<bool> IsAvailableAsync(CancellationToken ct);
}
```

**Provider configuration with Options pattern.**
Each provider has its own options class (`AzureOpenAIOptions`, `FoundryOptions`), bound from `appsettings.json` or environment variables. Clean separation of config from code.

### Code Walkthrough

#### AzureOpenAIModelClient (137 LOC)

```csharp
public sealed class AzureOpenAIModelClient : IModelClient
{
    public string ProviderName => "azure-openai";

    public AzureOpenAIModelClient(
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAIModelClient> logger) { ... }

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        // Uses Azure.AI.OpenAI SDK
        // Maps ChatMessage → OpenAI.Chat.ChatMessage
        // Returns structured ChatResponse with usage info
    }

    public async IAsyncEnumerable<ChatResponseChunk> StreamAsync(...)
    {
        // SDK streaming with await foreach
        // Yields ChatResponseChunk per token
    }
}
```

**Key points:**
- Uses the official `Azure.AI.OpenAI` NuGet package
- `MapMessages()` converts OpenClawNet's `ChatMessage` to SDK types
- Streaming uses `IAsyncEnumerable` — same pattern as the local LLM client
- Configuration via `AzureOpenAIOptions`: Endpoint, ApiKey, DeploymentName, Temperature, MaxTokens

#### FoundryModelClient (195 LOC)

```csharp
public sealed class FoundryModelClient : IModelClient
{
    public string ProviderName => "foundry";

    public FoundryModelClient(
        HttpClient httpClient,
        IOptions<FoundryOptions> options,
        ILogger<FoundryModelClient> logger) { ... }

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        // Custom HTTP POST to Foundry endpoint
        // Manual JSON serialization/deserialization
        // Maps Foundry-specific DTOs to ChatResponse
    }
}
```

**Key points:**
- No SDK — raw `HttpClient` with custom DTOs (`FoundryChatResponse`, `FoundryChoice`, etc.)
- `BuildPayload()` constructs the request body
- `EnsureConfigured()` validates endpoint/key before calls
- Shows what "different API shape" means in practice

#### DI Registration — Switching Providers

```csharp
// In Program.cs or startup — pick ONE:

// Option A: Local development
services.AddOllama(o => o.Model = "llama3.2");

// Option B: Azure OpenAI
services.AddAzureOpenAI(o => {
    o.Endpoint = config["AzureOpenAI:Endpoint"]!;
    o.ApiKey = config["AzureOpenAI:ApiKey"]!;
    o.DeploymentName = "gpt-4o";
});

// Option C: Microsoft Foundry
services.AddFoundry(o => {
    o.Endpoint = config["Foundry:Endpoint"]!;
    o.ApiKey = config["Foundry:ApiKey"]!;
    o.Model = "gpt-4o";
});
```

All three register as `IModelClient`. The agent, prompt composer, and tool loop never know which provider is active.

### Live Demo

1. Show the current local LLM-based chat working
2. Switch to Azure OpenAI (if available) by changing DI registration
3. Same chat interface, same tools, same skills — different cloud provider
4. Compare response quality and speed side by side
5. **Fallback:** If no Azure, show the configuration and explain — the code is the same either way

---

## Stage 2: Scheduling + Health (12 min)

### Concepts

**BackgroundService pattern.**
ASP.NET Core's `BackgroundService` runs tasks alongside your web app. No separate process, no Windows Service — just override `ExecuteAsync` and loop.

**Cron-based job scheduling.**
Users (or the agent itself) create jobs with cron expressions. The scheduler checks every 30 seconds for due jobs and runs them.

**Health checks + Aspire integration.**
Production apps need to answer: "Are you healthy?" ASP.NET Core health checks provide `/health` and `/alive` endpoints. Aspire Dashboard shows everything in one view.

### Code Walkthrough

#### JobSchedulerService

```csharp
public class JobSchedulerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            // Query DB: jobs where IsActive && NextRunTime <= now
            // For each due job:
            //   Create JobRun (Status=Running)
            //   Call orchestrator.RunAsync with synthetic request
            //   Update JobRun (Status=Success/Failed)
            //   Calculate NextRunTime from cron
        }
    }
}
```

**Key points:**
- Polls every 30 seconds — simple, reliable, no external scheduler needed
- Creates `JobRun` records for audit trail
- Uses the same `IAgentOrchestrator` as the chat — full tool/skill access
- Graceful shutdown via `CancellationToken`

#### SchedulerTool — The Agent Schedules Itself

```csharp
public sealed class SchedulerTool : ITool
{
    public string Name => "schedule";
    // Actions: "create", "list", "cancel"

    private async Task<ToolResult> CreateJobAsync(ToolInput input, ...)
    {
        // Parse: name, prompt, runAt (one-time) or cron (recurring)
        // Store ScheduledJob in database
        // Return confirmation with next run time
    }
}
```

This is powerful: a user says "Remind me every morning at 9 AM to check my calendar" and the agent calls the schedule tool to create a recurring job.

#### ServiceDefaults — Health + Telemetry

```csharp
public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
{
    builder.ConfigureOpenTelemetry();   // Metrics, tracing, logging
    builder.AddDefaultHealthChecks();   // /health, /alive
    // Service discovery, resilience handlers
}
```

**Health endpoints:**
- `GET /health` — Readiness probe (database, model provider)
- `GET /alive` — Liveness probe (process is running)

**OpenTelemetry:**
- ASP.NET Core metrics (request duration, error rates)
- HttpClient metrics (outbound call tracking)
- Runtime metrics (GC, thread pool)
- OTLP exporter for Aspire Dashboard

### Live Demo

1. Show the Aspire Dashboard — all services visible
2. Schedule a job via the API: "Check weather every hour"
3. Show the job in the database
4. Hit `GET /health` — show the JSON response
5. Point out OpenTelemetry traces in Aspire

---

## Stage 3: Testing + Production (12 min)

### Concepts

**Test pyramid: unit → integration → E2E.**
- **Unit tests (23):** Test individual components in isolation. Fast, reliable, no external dependencies.
- **Integration tests (1):** Test components working together. Uses real (in-memory) database.
- **E2E:** Manual demo — chat through the full flow.

**What to mock.**
- `IModelClient` — Don't call real AI in tests
- `IDbContextFactory` — Use EF Core in-memory provider
- `ITool` — Fake tools for executor tests
- `ISkillLoader` — Fake skill loader for composer tests

**Production checklist:**
- ✅ Health checks responding
- ✅ All tests passing
- ✅ Provider switching works
- ✅ Scheduled jobs executing
- ✅ OpenTelemetry exporting
- ✅ Error handling graceful

### Code Walkthrough

#### PromptComposerTests — Verify Skill Injection

```csharp
[Fact]
public async Task ComposeAsync_IncludesActiveSkills()
{
    // Arrange: FakeSkillLoader returns "code-review" skill
    // Act: composer.ComposeAsync(context)
    // Assert: system prompt contains skill name and content
}
```

4 tests covering: system prompt presence, skill injection, session summary, conversation history.

#### ToolExecutorTests — Approval Policy Enforcement

```csharp
[Fact]
public async Task ExecuteAsync_ReturnsFail_WhenToolNotFound()
{
    // Verifies graceful failure for unknown tools
}

[Fact]
public async Task ExecuteAsync_CallsTool_WhenFound()
{
    // Registers SuccessTool, executes, verifies output
}
```

3 tests covering: missing tool handling, successful execution, batch execution.

#### SkillParserTests — YAML Parsing Edge Cases

```csharp
[Fact]
public void Parse_WithValidFrontmatter_ExtractsMetadata()
{
    // Full YAML: name, description, category, tags
    // Verifies all fields extracted correctly
}

[Fact]
public void Parse_WithoutFrontmatter_UsesFileName()
{
    // Plain content → filename becomes skill name
}
```

4 tests covering: valid frontmatter, no frontmatter, disabled flag, empty content.

#### ConversationStoreTests — EF Core In-Memory

```csharp
public class ConversationStoreTests : IDisposable
{
    // Uses TestDbContextFactory with in-memory database
    // Each test gets a fresh Guid-named database

    [Fact]
    public async Task AddMessage_IncrementsOrderIndex()
    {
        // Adds 2 messages, verifies OrderIndex: 0, 1
    }
}
```

7 tests covering: create, get, add message, order index, list, delete, update title.

#### ToolRegistryTests — Registration + Lookup

5 tests covering: register, case-insensitive lookup, not found, get all, manifest.

### Live Demo

1. Run `dotnet test` → all 24 pass (23 unit + 1 integration)
2. Show test output with categories
3. Point out test patterns: Arrange/Act/Assert, fake implementations, in-memory DB

### 🤖 Copilot Moment — Write a New Test

**Context:** `ToolRegistryTests.cs` is open in the editor.

**Prompt to Copilot:**
> Write a new unit test for ToolRegistry that verifies registering a tool with a duplicate name overwrites the previous registration. Register two different FakeTool instances with the same name, then verify GetTool returns the second one.

**Expected:** Copilot generates a `[Fact]` test method that:
- Creates two `FakeTool` instances with the same `Name`
- Registers both
- Asserts `GetTool` returns the second instance

---

## Closing (14 min) — SERIES FINALE

### Full Platform Demo (5 min)

Walk through the entire platform end-to-end:
1. Start the app with Aspire
2. Open the chat — send a message (Session 1)
3. Use a tool — "What files are in the project?" (Session 2)
4. Toggle a skill — enable "code-review" mode (Session 3)
5. Schedule a job — "Remind me in 5 minutes" (Session 4)
6. Check health — `GET /health` (Session 4)
7. Show Aspire Dashboard — all services, traces, metrics

### Series Recap (4 min)

| Session | Topic | What We Built |
|---------|-------|--------------|
| **1** | Scaffolding + Local Chat | Aspire host, local LLM integration, gateway, chat UI |
| **2** | Tools + Agent Workflows | Tool interface, registry, executor, approval policies, tool loop |
| **3** | Skills + Memory | Markdown skills, YAML parsing, conversation summarization, semantic search |
| **4** | Automation + Cloud | Cloud providers, job scheduling, health checks, testing |

### Architecture Diagram

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

### Where to Go from Here

- **Custom tools:** Build domain-specific tools (Jira, GitHub, Slack)
- **Domain skills:** Create specialized skill packs for your team
- **Azure deployment:** Deploy to Azure Container Apps with Aspire
- **Advanced memory:** RAG with vector search, long-term knowledge
- **Multi-agent:** Agent-to-agent communication patterns
- **GitHub Copilot integration:** Use Copilot to extend the platform itself

### Thank You + Q&A

- Repository: `github.com/elbruno/openclawnet`
- Series: Microsoft Reactor — OpenClawNet
- Built with: .NET 10, Aspire, GitHub Copilot, Local LLMs (Ollama / Foundry Local)

---

## After the Session

### What Now Works

- ✅ Cloud provider switching (Local LLM → Azure OpenAI → Foundry)
- ✅ Background job scheduling with cron expressions
- ✅ Health check endpoints (`/health`, `/alive`)
- ✅ OpenTelemetry metrics and tracing
- ✅ 24 tests passing (23 unit + 1 integration)
- ✅ Complete AI agent platform — production-ready

### Key Concepts Covered

1. `IModelClient` polymorphism — one interface, multiple cloud providers
2. Options pattern for provider configuration
3. `BackgroundService` for long-running tasks
4. Cron-based job scheduling with audit trail
5. ASP.NET Core health checks and Aspire integration
6. OpenTelemetry for observability
7. Unit testing patterns: fakes, in-memory DB, Arrange/Act/Assert
8. Production readiness checklist

### Git Checkpoint

**Tag:** `session-4-complete`

**Files covered:**
- `src/OpenClawNet.Models.AzureOpenAI/` — Azure OpenAI client
- `src/OpenClawNet.Models.Foundry/` — Foundry client
- `src/OpenClawNet.Models.Abstractions/` — IModelClient interface
- `src/OpenClawNet.Tools.Scheduler/` — SchedulerTool
- `src/OpenClawNet.Gateway/Services/JobSchedulerService.cs` — Background scheduler
- `src/OpenClawNet.ServiceDefaults/` — Health checks + telemetry
- `tests/OpenClawNet.UnitTests/` — 23 unit tests
- `tests/OpenClawNet.IntegrationTests/` — 1 integration test
