# Jobs + Skills Architecture Analysis
## Using Microsoft Agent Framework as Primary Runtime

**Author:** Ripley (Lead / Architect)  
**Date:** 2026-04-18  
**Status:** Architecture Proposal  
**Branch:** `analysis/jobs-skills-and-architecture-review`

---

## Executive Summary

This document presents two related architecture proposals:

1. **Jobs + Agent Profiles Binding** — A domain model for scheduled/triggered Job execution bound to Agent Profiles
2. **MAF-Driven Runtime** — Migration from current `IModelClient`/tool framework to Microsoft Agent Framework (MAF) as the primary orchestration layer

**Key Recommendation:** MAF is the right choice. It's production-ready (1.0 GA), aligns with Microsoft's .NET AI strategy, provides native Workflow orchestration, and eliminates custom abstractions. Jobs naturally map to MAF Workflows; Skills become MAF tools (AIFunction).

---

## Table of Contents

1. [Proposal 1: Jobs + Agent Profiles Domain Model](#proposal-1-jobs--agent-profiles-domain-model)
2. [Proposal 2: MAF as Primary Runtime](#proposal-2-maf-as-primary-runtime)
3. [Current State Analysis](#current-state-analysis)
4. [MAF Mapping & Architecture](#maf-mapping--architecture)
5. [Migration Strategy](#migration-strategy)
6. [Code Sketches](#code-sketches)
7. [Open Decisions](#open-decisions)

---

## Proposal 1: Jobs + Agent Profiles Domain Model

### What is a Job?

A **Job** is a scheduled or triggered unit of agent work executed with a specific Agent Profile. It's the automation primitive in OpenClawNet — the bridge between time-based scheduling, event triggers, and agent execution.

```
┌─────────────────────────────────────────────────────────────────┐
│  Job Definition                                                 │
│  ─────────────────────────────────────────────────────────────  │
│  • Name, Prompt (the instruction for the agent)                 │
│  • AgentProfileName (FK) — which agent personality/config       │
│  • Schedule (Cron, one-shot, or manual)                         │
│  • Lifecycle state (Draft → Active → Paused/Cancelled)          │
│  • Inputs (JSON payload for parameterized prompts)              │
│  • Outputs (last run result, success/error)                     │
│  • Triggers (manual, cron, webhook)                             │
└─────────────────────────────────────────────────────────────────┘
```

### Domain Model

#### Core Entity: `ScheduledJob` (already exists)

Current schema (from `OpenClawNet.Storage.Entities.ScheduledJob`):

```csharp
public sealed class ScheduledJob
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Prompt { get; set; }
    public string? CronExpression { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public JobStatus Status { get; set; }  // Draft, Active, Paused, Cancelled, Completed
    public bool IsRecurring { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string? TimeZone { get; set; }
    public string? NaturalLanguageSchedule { get; set; }
    public bool AllowConcurrentRuns { get; set; }
    
    // ✅ ALREADY PRESENT — Agent Profile binding
    public string? AgentProfileName { get; set; }
    
    public List<JobRun> Runs { get; set; } = [];
}
```

**✅ Good news:** The `AgentProfileName` FK already exists! The binding is in place.

#### Additions Needed

1. **Structured Inputs/Outputs**

```csharp
// Add to ScheduledJob:
public string? InputParametersJson { get; set; }  // JSON dictionary for template substitution
public string? LastOutputJson { get; set; }       // Last successful run result (for chaining)
```

2. **Trigger Metadata** (for webhook/event triggers)

```csharp
// Add to ScheduledJob:
public string? TriggerType { get; set; }  // "cron" | "manual" | "webhook" | "event"
public string? WebhookEndpoint { get; set; }  // If TriggerType == "webhook", this is the unique URL
```

3. **Enhanced JobRun** (already solid, minor additions)

```csharp
public sealed class JobRun
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string Status { get; set; }  // "running", "completed", "failed"
    public string? Result { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Additions:
    public string? InputSnapshotJson { get; set; }  // Snapshot of inputs at execution time
    public int TokensUsed { get; set; }             // For cost tracking
    public string? ExecutedByAgentProfile { get; set; }  // Which profile was used (if job config changed)
    
    public ScheduledJob Job { get; set; } = null!;
}
```

### Job ↔ AgentProfile Relationship

```
┌──────────────────────┐       AgentProfileName (FK)       ┌─────────────────────────┐
│   ScheduledJob       │ ────────────────────────────────> │   AgentProfile          │
│   ────────────────   │                                   │   ──────────────────    │
│   • Name             │  Each Job MUST reference a        │   • Name (PK)           │
│   • Prompt           │  profile at execution time.       │   • Provider            │
│   • AgentProfileName │                                   │   • Model               │
│   • Schedule         │  Profile defines:                 │   • Instructions        │
│   • Status           │  - Which provider/model           │   • EnabledTools        │
└──────────────────────┘  - Instructions (system prompt)   │   • Temperature, etc.   │
                          - Tools/Skills available          └─────────────────────────┘
                          - Model parameters
```

**Resolution Rules:**

1. **At Job Execution Time:**
   - Resolve `AgentProfileName` → `AgentProfile` entity (from DB or in-memory store)
   - If `AgentProfileName == null`, use the **default profile** (where `IsDefault == true`)
   - If profile not found, fail with clear error

2. **Versioning (Future):**
   - Current design: Jobs reference profiles by name (late-binding, always gets latest config)
   - Future enhancement: Add `AgentProfileVersion` FK to snapshot the exact profile config at job creation time

### Lifecycle States

Current `JobStatus` enum (from `OpenClawNet.Storage.Entities.JobStatus`):

```csharp
public enum JobStatus
{
    Draft,      // Created but not activated
    Active,     // Eligible for scheduler polling
    Paused,     // Temporarily disabled
    Cancelled,  // Permanently stopped
    Completed   // One-shot job finished
}
```

**State Transitions:**

```
Draft ──(activate)──> Active ──(pause)──> Paused
  │                     │                   │
  │                     └──(cancel)──> Cancelled
  │                     └──(complete)──> Completed (one-shot only)
  │
  └─────(cancel)──> Cancelled
```

**Transitions Already Implemented** (per `.squad/agents/ripley/history.md`):
- 7 valid transitions enforced by `JobStatusTransitions` guard class
- 3 action endpoints: `/start`, `/pause`, `/resume` (+ existing `/cancel`)

### Execution Context

When a Job executes, the runtime creates:

```csharp
public sealed class JobExecutionContext
{
    public Guid JobId { get; init; }
    public Guid RunId { get; init; }
    public string Prompt { get; init; }
    public AgentProfile ResolvedProfile { get; init; }
    public IReadOnlyDictionary<string, object> InputParameters { get; init; }
    public DateTime TriggeredAt { get; init; }
    public string TriggerSource { get; init; }  // "scheduler" | "manual" | "webhook-abc123"
}
```

Execution flow:

1. **Scheduler/Trigger** creates a `JobRun` with `Status = "running"`
2. Resolves `AgentProfile` via `AgentProfileName` FK
3. Composes prompt with input substitution (if `InputParametersJson` present)
4. Invokes `IAgentOrchestrator.ProcessIsolatedAsync()` with a clean session
5. Writes result to `JobRun.Result`, updates `Status = "completed"/"failed"`
6. Optionally emits event for downstream job chaining (see Open Decisions)

### Persistence & Results

- **Job Definitions:** Stored in `ScheduledJobs` table (SQLite via EF Core)
- **Job Runs:** Stored in `JobRuns` table (many-to-one with `ScheduledJobs`)
- **Result Retention:** Last 50 runs per job (configurable); older runs purged by background task
- **Observability:** Each run logs to structured logging + optional telemetry (OpenTelemetry)

### API Surface

Current endpoints (from `JobEndpoints.cs`):

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET    | `/api/jobs` | List all jobs (paginated, sorted by `CreatedAt` desc) |
| POST   | `/api/jobs` | Create a new job (default status: `Draft`) |
| GET    | `/api/jobs/{jobId}` | Get job detail with run history |
| PUT    | `/api/jobs/{jobId}` | Update job (only when `Draft` or `Paused`) |
| DELETE | `/api/jobs/{jobId}` | Delete job |

**Additions Needed:**

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST   | `/api/jobs/{jobId}/execute` | **Manual trigger** — creates a `JobRun` immediately |
| POST   | `/api/jobs/{jobId}/start` | State transition: `Draft → Active` |
| POST   | `/api/jobs/{jobId}/pause` | State transition: `Active → Paused` |
| POST   | `/api/jobs/{jobId}/resume` | State transition: `Paused → Active` |
| POST   | `/api/jobs/{jobId}/cancel` | State transition: `* → Cancelled` |
| GET    | `/api/jobs/{jobId}/runs` | Paginated run history for a job |
| GET    | `/api/jobs/{jobId}/runs/{runId}` | Single run detail with full result/error |

### UI Implications (Blazor)

**New Pages Needed:**

1. **`/jobs`** — Job list page
   - Table: Name, Profile, Schedule, Status, Last Run, Next Run, Actions
   - Actions: Start, Pause, Resume, Cancel, Edit, Delete, Execute Now

2. **`/jobs/create`** — Job creation wizard
   - Step 1: Name + Prompt (with template variable highlighting)
   - Step 2: Agent Profile selector (dropdown, shows provider/model)
   - Step 3: Schedule (cron builder UI or natural language parser)
   - Step 4: Review & Create

3. **`/jobs/{id}`** — Job detail page
   - Configuration summary (editable if `Draft` or `Paused`)
   - Run history timeline (last 20 runs)
   - Manual trigger button
   - State action buttons (Start/Pause/Resume/Cancel)

4. **`/jobs/{id}/runs/{runId}`** — Run detail page
   - Full execution log
   - Input/output JSON viewers
   - Token usage, duration, error stacktrace (if failed)

**Shared Components:**

- `<CronBuilder />` — Visual cron expression editor
- `<AgentProfileSelector />` — Dropdown with profile details
- `<JobStatusBadge status="active" />` — Color-coded status pill
- `<PromptTemplateEditor />` — Monaco editor with `{{variable}}` syntax highlighting

---

## Proposal 2: MAF as Primary Runtime

### Why MAF?

Microsoft Agent Framework (1.0 GA, released 2025) is purpose-built for agent orchestration. Using it eliminates 90% of our custom abstraction layer and gives us:

1. **Native Workflow Orchestration** — Jobs → Workflows with executors, edges, conditional routing
2. **Standard Tool Protocol** — Skills → `AIFunction` via `AIFunctionFactory.Create()`
3. **Provider Ecosystem** — First-party support for Ollama, Azure OpenAI, Foundry, GitHub Copilot SDK
4. **Production Features** — Middleware, context providers, compaction, observability, AG-UI hosting
5. **Future-Proof** — Microsoft's official .NET AI runtime; updated with Azure AI Foundry releases

**What We Keep:**
- `AgentProfile` abstraction (configuration layer above MAF)
- `IToolRegistry` + `ITool` (our domain-specific tools)
- Job scheduler infrastructure
- Blazor UI + API layer

**What Changes:**
- `IModelClient` → deleted (replaced by MAF's `IChatClient` + `IAgentProvider`)
- Custom tool loop in `DefaultAgentRuntime` → deleted (MAF handles it)
- Skill injection → becomes MAF `AIContextProvider` (already partially done via `AgentSkillsProvider`)

---

## Current State Analysis

### How Skills Work Today

**File Format:** Markdown with YAML front-matter (borrowed from GitHub Copilot CLI patterns)

```markdown
---
name: dotnet-assistant
description: Helps with .NET development tasks
category: development
enabled: true
tags:
  - dotnet
  - csharp
---

You are a .NET development assistant. When helping with .NET code:
- Prefer modern C# conventions (nullable reference types, records, pattern matching)
- Suggest async/await patterns where appropriate
...
```

**Discovery & Loading:**

1. `FileSkillLoader` scans `skills/built-in/`, `skills/samples/`, `skills/installed/`
2. `SkillParser.Parse()` extracts YAML metadata + body content
3. Skills cached in-memory; reloadable via `/api/skills/reload`

**Injection into Agent:**

Current approach (from `DefaultAgentRuntime`):

```csharp
var agentOptions = new ChatClientAgentOptions
{
    AIContextProviders = [agentSkillsProvider],  // <-- MAF's AgentSkillsProvider
    ChatOptions = new ChatOptions { Tools = _toolAIFunctions }
};
_chatClientAgent = new ChatClientAgent(_adapter, agentOptions, loggerFactory, null);
```

**✅ Already using MAF!** `AgentSkillsProvider` is from `Microsoft.Agents.AI` — it's a built-in MAF context provider that discovers skill files and injects them as system messages.

### How Tools Work Today

**Definition:** `ITool` interface (from `OpenClawNet.Tools.Abstractions`)

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolMetadata Metadata { get; }
    Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default);
}
```

**Registration:** `IToolRegistry` (singleton) — all tools register at startup via DI

**Invocation:**

1. Model returns tool calls (via `IChatClient.CompleteAsync`)
2. `DefaultAgentRuntime` extracts tool calls from response
3. Calls `IToolExecutor.ExecuteAsync(toolName, args)` — looks up tool in registry
4. Appends results to conversation, loops back to model

**Wrapping for MAF:**

Current bridge (from `ToolAIFunction.cs`):

```csharp
internal sealed class ToolAIFunction : AIFunction
{
    private readonly ITool _tool;
    
    public override string Name => _tool.Name;
    public override string Description => _tool.Description;
    public override JsonElement JsonSchema => _tool.Metadata.ParameterSchema.RootElement;
    
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(arguments.ToDictionary(...));
        var input = new ToolInput { ToolName = _tool.Name, RawArguments = json };
        var result = await _tool.ExecuteAsync(input, ct);
        return result.Success ? result.Output : $"Error: {result.Error}";
    }
}
```

**✅ Already MAF-compatible!** Tools are wrapped as `AIFunction` and passed to `ChatOptions.Tools`.

### Current Runtime Flow

```
User Message
    ↓
AgentOrchestrator.ProcessAsync()
    ↓
DefaultAgentRuntime.ExecuteAsync()
    ↓
1. Load conversation history (IConversationStore)
2. Compose prompt with system message + skills (IPromptComposer)
3. Wrap tools as AIFunction[] (ToolAIFunction)
4. Call ChatClientAgent.RunAsync() — FIRST call uses AgentSkillsProvider for progressive disclosure
5. Parse tool calls from response
6. Execute tools via IToolExecutor (manual loop, max 10 iterations)
7. Append results, call ChatClient again (directly, no AgentSkillsProvider for tool loops)
8. Return final response + persist to conversation store
```

**Assessment:** 80% MAF, 20% custom. The tool loop is manual. MAF can do this natively if we let `FunctionInvokingChatClient` wrap the chat client.

---

## MAF Mapping & Architecture

### Core Primitives Mapping

| OpenClawNet Concept | MAF Primitive | Notes |
|---------------------|---------------|-------|
| **AgentProfile** | No direct equivalent | We keep this as config/DI layer above MAF |
| **Job (scheduled)** | `Workflow` | Job definition → `WorkflowBuilder` graph |
| **Job Execution** | `InProcessExecution.RunAsync(workflow, input)` | Creates a `Run`, manages state |
| **Skill (file-based)** | `AIContextProvider` (`AgentSkillsProvider`) | ✅ Already using MAF's built-in provider |
| **Tool (ITool)** | `AIFunction` (wrapped via `ToolAIFunction`) | ✅ Already wrapped; can stay or migrate to `AIFunctionFactory.Create()` |
| **Agent conversation** | `AIAgent` + `AgentSession` | MAF manages state via sessions/threads |
| **Streaming chat** | `AIAgent.RunStreamingAsync()` | Token-by-token yields via `IAsyncEnumerable<AgentResponseUpdate>` |
| **Provider (Ollama, Azure, Foundry)** | `IAgentProvider.AsAIAgent()` or `.AsIChatClient()` | First-party extensions in MAF |

### MAF Agent Creation Patterns

#### Pattern 1: IChatClient → AIAgent (simple agents)

```csharp
// Current (via our IAgentProvider abstraction):
IChatClient chatClient = agentProvider.CreateChatClient(profile);
var agent = new ChatClientAgent(chatClient, instructions: profile.Instructions);

// MAF-native (using AsAIAgent extension):
AIAgent agent = chatClient.AsAIAgent(
    name: profile.Name,
    instructions: profile.Instructions,
    tools: toolFunctions
);
```

**✅ Simpler.** No need for `ChatClientAgent` wrapper.

#### Pattern 2: Provider → AIAgent (direct)

```csharp
// Ollama (via OllamaSharp):
AIAgent agent = ollamaClient.AsAIAgent(
    model: "llama3.2",
    instructions: "You are helpful."
);

// Azure OpenAI (via Azure.AI.Projects):
AIAgent agent = new AIProjectClient(endpoint, credential)
    .AsAIAgent(
        model: "gpt-4o-mini",
        name: "HelpfulAssistant",
        instructions: "You are a helpful assistant."
    );

// Foundry Agent Service (hosted runtime):
AIAgent agent = new AIProjectClient(endpoint, credential)
    .AsFoundryAgent(agentId: "my-agent");
```

**Foundry Local (NOT supported in .NET MAF):**
- MAF only supports Foundry Agent Service (cloud/on-prem deployment)
- Foundry Local is Python-only (no .NET client SDK)
- **Mitigation:** Keep existing `FoundryLocalModelClient` as a bridge, wrap as `IChatClient`, then `.AsAIAgent()`

### Skills as MAF Tools (AIFunction)

**Option A: Keep file-based skills as `AIContextProvider`** (current approach)

```csharp
// AgentSkillsProvider scans skills/ directory, injects as system messages
var agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
{
    AIContextProviders = [new AgentSkillsProvider(skillsPath)],
    ChatOptions = new ChatOptions { Tools = toolFunctions }
});
```

**Pros:** Already working; skills are declarative markdown files  
**Cons:** Skills are "instructions" not "tools" — model can't explicitly invoke them

**Option B: Migrate skills to AIFunction** (typed code functions)

```csharp
// Define skill as a C# function with [Description] attributes
[Description("Helps with .NET development tasks")]
static string DotNetAssistant(string question)
{
    return "Prefer modern C# conventions (nullable reference types, records)...";
}

// Register as tool:
var dotnetTool = AIFunctionFactory.Create(DotNetAssistant);
var agent = chatClient.AsAIAgent(tools: [dotnetTool, weatherTool, ...]);
```

**Pros:** Skills become first-class tools; model can choose when to invoke  
**Cons:** Loses markdown file simplicity; harder for non-devs to add skills

**Recommendation:** **Option A (keep current)** for built-in skills. Skills are system-message enrichment, not tools. True tools (like `SchedulerTool`, `FileSystemTool`) remain as `ITool` → `AIFunction`.

### Jobs as MAF Workflows

**Job Definition → Workflow Graph**

A Job is a workflow with a single agent executor:

```csharp
// Simple job: "Summarize news headlines every morning"
var summarizerAgent = chatClient.AsAIAgent(
    instructions: "Summarize the given text into 3 bullet points."
);

var workflow = new WorkflowBuilder(summarizerAgent)
    .WithOutputFrom(summarizerAgent)
    .Build();

// Execute job:
var input = new ChatMessage(ChatRole.User, fetchedNewsText);
Run run = await InProcessExecution.RunAsync(workflow, input);
var result = run.GetOutputData<string>();
```

**Complex Job: Multi-step pipeline**

Job: "Analyze email → route spam → draft reply"

```csharp
var spamDetectorAgent = chatClient.AsAIAgent(...);
var emailAssistantAgent = chatClient.AsAIAgent(...);
var spamHandlerExecutor = new SpamHandlerExecutor();

var workflow = new WorkflowBuilder(spamDetectorAgent)
    .AddSwitch(spamDetectorAgent, sw => sw
        .AddCase(r => r.IsSpam == false, emailAssistantAgent)
        .AddCase(r => r.IsSpam == true, spamHandlerExecutor)
        .WithDefault(spamHandlerExecutor)
    )
    .WithOutputFrom(emailAssistantAgent, spamHandlerExecutor)
    .Build();
```

**Job Execution Context in MAF:**

```csharp
// When scheduler triggers a job:
var jobDef = await db.Jobs.FindAsync(jobId);
var profile = await agentProfileStore.GetByNameAsync(jobDef.AgentProfileName);

// Create agent from profile
AIAgent agent = CreateAgentFromProfile(profile);

// Build single-executor workflow
var workflow = new WorkflowBuilder(agent).WithOutputFrom(agent).Build();

// Execute with job prompt + input parameters
var inputMsg = ComposeJobInput(jobDef.Prompt, jobDef.InputParametersJson);
Run run = await InProcessExecution.RunAsync(workflow, inputMsg);

// Persist result
var output = run.GetOutputData<string>();
await UpdateJobRun(runId, output);
```

**Benefits:**

1. **Observability:** MAF emits OpenTelemetry spans for each executor + edge
2. **Checkpointing:** Workflows support pause/resume (future feature for long-running jobs)
3. **Sub-workflows:** Complex jobs can compose smaller workflows
4. **Type-safe routing:** Conditional edges with strongly-typed message contracts

---

## Migration Strategy

### Phase 1: Add MAF-Native IAgentProvider (No Breaking Changes)

**Goal:** Introduce new provider pattern alongside existing `IModelClient`, prove it works.

**Tasks:**

1. Create `IAgentProvider` interface (already drafted in history):

```csharp
public interface IAgentProvider
{
    string ProviderName { get; }
    AIAgent CreateAgent(AgentProfile profile, IReadOnlyList<AIFunction> tools);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
```

2. Implement providers:
   - `OllamaAgentProvider` (uses `OllamaSharp`)
   - `AzureOpenAIAgentProvider` (uses `Azure.AI.Projects`)
   - `FoundryAgentProvider` (uses `AIProjectClient.AsFoundryAgent()`)
   - `FoundryLocalAgentProvider` (bridge via existing `FoundryLocalModelClient`)
   - `GitHubCopilotAgentProvider` (uses `GitHub.Copilot.SDK`)

3. Register in DI alongside existing providers (no cutover yet)

4. Add `RuntimeAgentProvider` router (like existing `RuntimeModelClient`)

5. Test via unit tests + manual Blazor UI validation

**Effort:** 3–4 days (5 providers × ~4 hours each + router + tests)

### Phase 2: Cutover DefaultAgentRuntime to Use IAgentProvider

**Goal:** Replace `IModelClient` + manual tool loop with MAF's `AIAgent.RunAsync()`.

**Tasks:**

1. Update `DefaultAgentRuntime`:
   - Remove `ModelClientChatClientAdapter` wrapper
   - Remove manual tool loop (10-iteration logic)
   - Call `AIAgent.RunAsync()` directly — MAF handles tools via `FunctionInvokingChatClient`

2. Update `AgentOrchestrator` to resolve `IAgentProvider` instead of `IModelClient`

3. Update agent profile endpoints to show provider-specific fields (Foundry project ID, etc.)

4. Run full test suite (246 tests) + Playwright E2E tests (39 tests)

**Effort:** 1–2 days (refactor + test validation)

### Phase 3: Remove Legacy IModelClient (Clean-Up)

**Goal:** Delete deprecated code, consolidate on MAF.

**Tasks:**

1. Delete `IModelClient`, `RuntimeModelClient`, `ModelClientChatClientAdapter`
2. Delete custom DTOs: `ChatMessage`, `ChatResponse`, `ChatRequest`, `ChatResponseChunk`, `ToolDefinition`, `ToolCall`, `UsageInfo` (all replaced by `Microsoft.Extensions.AI` types)
3. Update all references to use MAF types
4. Update Session guides/docs to reflect MAF terminology

**Effort:** 1 day (mechanical deletion + docs)

### Backward Compatibility

**Session 1-4 Attendee Materials:**

- **Impact:** Low. Session 1 already uses `IAgentProvider` (per recent migration). Sessions 2-4 use high-level orchestrator APIs.
- **Action:** Update slides to mention "powered by Microsoft Agent Framework" (marketing win).

**Existing Jobs/Schedules:**

- **Impact:** None. Job definitions are data; runtime change is transparent.
- **Action:** None required.

---

## Code Sketches

### Job Execution with MAF Workflow

```csharp
// JobExecutor.cs (new service)
public sealed class JobExecutor
{
    private readonly IAgentProfileStore _profileStore;
    private readonly IAgentProvider _agentProvider;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<JobExecutor> _logger;

    public async Task<JobRunResult> ExecuteJobAsync(ScheduledJob job, CancellationToken ct)
    {
        // 1. Resolve agent profile
        var profile = await _profileStore.GetByNameAsync(job.AgentProfileName ?? "default", ct);
        if (profile == null)
            throw new InvalidOperationException($"Agent profile '{job.AgentProfileName}' not found.");

        // 2. Create MAF agent from profile
        var tools = _toolRegistry.GetAllTools().Select(t => new ToolAIFunction(t)).ToArray();
        AIAgent agent = _agentProvider.CreateAgent(profile, tools);

        // 3. Build single-executor workflow
        var workflow = new WorkflowBuilder(agent)
            .WithOutputFrom(agent)
            .Build();

        // 4. Compose input message (prompt + parameter substitution)
        var inputMsg = ComposeJobInput(job.Prompt, job.InputParametersJson);

        // 5. Execute workflow
        var sw = Stopwatch.StartNew();
        Run run = await InProcessExecution.RunAsync(workflow, inputMsg, ct);
        sw.Stop();

        // 6. Extract result
        var output = run.GetOutputData<string>();
        
        return new JobRunResult
        {
            Status = "completed",
            Result = output,
            DurationMs = sw.ElapsedMilliseconds
        };
    }

    private ChatMessage ComposeJobInput(string promptTemplate, string? inputJson)
    {
        // Simple {{variable}} substitution
        var prompt = promptTemplate;
        if (!string.IsNullOrEmpty(inputJson))
        {
            var inputs = JsonSerializer.Deserialize<Dictionary<string, object>>(inputJson);
            foreach (var kv in inputs)
                prompt = prompt.Replace($"{{{{{kv.Key}}}}}", kv.Value.ToString());
        }
        return new ChatMessage(ChatRole.User, prompt);
    }
}
```

### Skill as AIFunction (Optional Migration)

```csharp
// DotNetAssistantSkill.cs
public static class DotNetAssistantSkill
{
    [Description("Provides guidance on .NET development best practices")]
    public static string GetDotNetAdvice(
        [Description("The .NET development question")] string question)
    {
        return @"
When writing .NET code:
- Prefer modern C# conventions (nullable reference types, records, pattern matching)
- Use async/await for I/O operations
- Follow dependency injection patterns
- Use minimal APIs for new web services
";
    }
}

// Registration:
var dotnetTool = AIFunctionFactory.Create(
    DotNetAssistantSkill.GetDotNetAdvice,
    name: "dotnet-assistant"
);
```

### Agent Profile → AIAgent Factory

```csharp
public sealed class AgentFactory
{
    private readonly IAgentProvider _agentProvider;
    private readonly IToolRegistry _toolRegistry;

    public AIAgent CreateFromProfile(AgentProfile profile)
    {
        // Gather tools
        var enabledToolNames = profile.EnabledTools?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var tools = _toolRegistry.GetTools(enabledToolNames)
            .Select(t => (AITool)new ToolAIFunction(t))
            .ToList();

        // Delegate to provider
        return _agentProvider.CreateAgent(profile, tools);
    }
}
```

---

## Resolved Decisions

**Decided by:** Bruno Capuano (Product Owner)  
**Date:** 2026-04-18  
**Architect:** Ripley

All seven open architecture decisions have been resolved. Implementation plan updated to reflect these choices.

### 1. Job Complexity ✅ RESOLVED

**Decision:** **Single-agent jobs (v1)**, add **DAG workflows as 'Advanced Jobs' post-Session-4**

**Rationale:** Educational context requires simple, teachable patterns for Session 4. Single-agent jobs (one agent, one prompt, one execution) are easy to configure via UI and explain in a live demo. DAG workflows unlock powerful multi-step automation but add UI and conceptual complexity better suited for advanced track.

**Implementation:** v1 ships with single-executor MAF Workflows. DAG workflow editor + multi-step Job definitions deferred to post-Session-4 "Advanced Jobs" bucket.

### 2. Job Chaining ✅ RESOLVED

**Decision:** **Event emission as future enhancement**, tracked in backlog

**Rationale:** Job → Job chaining (Job A completion triggers Job B with output as input) is valuable but not critical for Session 4 delivery. Requires event infrastructure (event bus, subscription model) not yet built. Manual output copying is acceptable for v1.

**Implementation:** No chaining support in v1. Add to backlog for post-Session-4 implementation (estimated 2-3 days). Track as "Event-Driven Job Chaining" feature.

### 3. Skills Format ✅ RESOLVED

**Decision:** **Keep markdown for built-in skills (v1)**; **AIFunction SDK for 'power-user skills' (future)**

**Rationale:** Current file-based skills (markdown → `AIContextProvider` system message enrichment) work well and are accessible to non-developers. Code-based `AIFunction` skills require C# knowledge but enable selective invocation by model (first-class tools). Both have value.

**Implementation:** v1 keeps all built-in skills as markdown files. Post-Session-4, add "Power-User Skill SDK" allowing C# developers to write typed AIFunction skills. Document both patterns.

### 4. Foundry Local Support ✅ RESOLVED

**Decision:** **Keep the bridge** (wrap `FoundryLocalModelClient` as `IChatClient` → `.AsAIAgent()`)

**Rationale:** Python MAF has native Foundry Local support; .NET MAF does not. Our existing `FoundryLocalModelClient` bridge is a differentiator and preserves local-first option for users without Azure subscriptions. Maintenance cost is minimal.

**Implementation:** v1 keeps `FoundryLocalAgentProvider` as one of the 5 IAgentProvider implementations. Document as "OpenClawNet-specific extension" in architecture docs.

### 5. Job Versioning ✅ RESOLVED

**Decision:** **Late-binding (always resolve latest profile) for v1**; add snapshot versioning in v2 if requested

**Rationale:** Late-binding (Jobs resolve `AgentProfile` at execution time) is simple and ensures one source of truth. Profile changes immediately affect all jobs using that profile. Risk: profile edit can break jobs. Mitigation: Users can clone profiles before editing. Snapshot versioning (Job stores profile config copy at creation) adds complexity without proven user need.

**Implementation:** v1 uses late-binding (`Job.AgentProfileName` FK, resolved at runtime). Add profile versioning + snapshot-on-creation in v2 if users report issues with profile drift.

### 6. Dry-Run Execution ✅ RESOLVED

**Decision:** **Required for v1** — implement `/api/jobs/{id}/dry-run` endpoint

**Rationale:** Essential for Session 4 educational context. Attendees need to test Jobs safely without persisting JobRun records or triggering unintended side effects. Dry-run executes job, returns result, but doesn't save to database. Still consumes model tokens (use cheap Ollama models for demos).

**Implementation:** Add `POST /api/jobs/{id}/dry-run` endpoint to Gateway API. Returns `{ "status": "success", "output": "...", "tokensUsed": 42, "durationMs": 1234 }`. Document in Session 4 guide as testing best practice.

### 7. Token Tracking ✅ RESOLVED

**Decision:** **Per-run tracking (v1)** PLUS add **`/api/jobs/{id}/stats` endpoint for on-demand aggregation**

**Rationale:** Fine-grained tracking (`JobRun.TokensUsed`) is easy to implement and preserves run-level detail. Aggregate tracking (`ScheduledJob.TotalTokensUsed`) provides quick cost visibility but requires database triggers or update logic on every JobRun save. Hybrid approach: track per-run, compute aggregates on-demand via stats endpoint.

**Implementation:** 
- v1: Add `JobRun.TokensUsed` column (already proposed in entity schema)
- v1: Add `GET /api/jobs/{id}/stats` endpoint returning `{ "totalRuns": 42, "totalTokens": 15000, "avgTokensPerRun": 357, "totalCostUsd": 0.045 }` (computed from JobRuns table)
- Future: Consider `ScheduledJob.TotalTokensUsed` cached column if stats queries become slow

---

## Implementation Plan

### Execution-Ready Roadmap

This plan reflects the 7 resolved decisions above. Work is sequenced across 3 phases, with explicit session boundaries (Session 3 vs Session 4 vs post-Session-4) and clear deliverables for code, tests, docs, and infrastructure.

---

### Phase 1: MAF Foundation (Session 3) — 3-4 days

**Goal:** Add `IAgentProvider` abstraction + 5 provider implementations. No breaking changes to existing runtime.

#### Code Work

| File/Component | Changes | Owner | PR Size |
|----------------|---------|-------|---------|
| `OpenClawNet.Runtime/Abstractions/IAgentProvider.cs` | New interface: `CreateAgent(profile, tools) → AIAgent`, `IsAvailableAsync()`, `ProviderName` property | Kane | Small |
| `OpenClawNet.Runtime/Providers/OllamaAgentProvider.cs` | Implement using `OllamaSharp` SDK → `.AsAIAgent()` extension | Kane | Medium |
| `OpenClawNet.Runtime/Providers/AzureOpenAIAgentProvider.cs` | Implement using `Azure.AI.Projects` → `AIProjectClient.AsFoundryAgent()` | Kane | Medium |
| `OpenClawNet.Runtime/Providers/FoundryAgentProvider.cs` | Implement using `Azure.AI.Projects` → `AIProjectClient.AsFoundryAgent()` for cloud | Kane | Medium |
| `OpenClawNet.Runtime/Providers/FoundryLocalAgentProvider.cs` | **Bridge:** Wrap existing `FoundryLocalModelClient` as `IChatClient` → `.AsAIAgent()` | Kane | Small |
| `OpenClawNet.Runtime/Providers/GitHubCopilotAgentProvider.cs` | Implement using `GitHub.Copilot.SDK` (local CLI auth, no keys) | Kane | Medium |
| `OpenClawNet.Runtime/RuntimeAgentProvider.cs` | Gateway router: select provider by `AgentProfile.ProviderType`, delegate to `IAgentProvider` | Kane | Medium |
| `OpenClawNet.Gateway/DependencyInjection.cs` | Register all 5 providers + `RuntimeAgentProvider` in DI | Kane | Small |
| `OpenClawNet.Storage/Entities/AgentProfile.cs` | Add optional fields: `FoundryProjectId`, `FoundryDeploymentName`, `CopilotModel` | Bishop | Small |
| `OpenClawNet.Storage/Migrations/AddAgentProfileProviderFields.cs` | EF Core migration for new columns | Bishop | Small |

**Total:** ~10 files, 3-4 PRs

#### Tests (Ash)

- **Unit Tests:** `IAgentProvider` interface contract tests (mock `AIAgent` creation)
- **Provider Tests:** One test per provider verifying `CreateAgent()` returns valid `AIAgent` (mocked dependencies)
- **Integration Tests:** `RuntimeAgentProvider` routing logic (5 providers × 1 test each)
- **Coverage Target:** 85%+ for new provider code

#### Docs (Parker)

- **New:** `/docs/architecture/agent-providers.md` — IAgentProvider pattern, 5 provider implementations, configuration guide
- **Update:** `/docs/sessions/session-3/guide.md` — Add "Agent Providers" section explaining abstraction (optional deep-dive)
- **Update:** `README.md` — Add "Supported Providers" section listing Ollama, Azure OpenAI, Foundry, Foundry Local, GitHub Copilot

#### Aspire/Infra (Bishop)

- **NuGet Packages:** Add `Microsoft.Agents.AI.OpenAI`, `Microsoft.Agents.AI.Foundry`, `Microsoft.Agents.AI.GitHub.Copilot`, `OllamaSharp` to `OpenClawNet.Runtime.csproj`
- **AppHost:** No changes (providers use existing services)

#### Exit Criteria

- [ ] All 5 providers implemented and registered in DI
- [ ] Unit + integration tests pass (85%+ coverage)
- [ ] Documentation merged
- [ ] No breaking changes to existing `IModelClient` runtime (both runtimes coexist)

---

### Phase 2: MAF Cutover + Jobs Domain (Session 3/4 boundary) — 2-3 days

**Goal:** Cutover `DefaultAgentRuntime` to use MAF's `AIAgent.RunAsync()`. Add Jobs domain model + execution APIs.

#### Code Work (Runtime Cutover)

| File/Component | Changes | Owner | PR Size |
|----------------|---------|-------|---------|
| `OpenClawNet.Runtime/DefaultAgentRuntime.cs` | **Replace:** Remove `IModelClient` dependency + manual tool loop. **Add:** Inject `IAgentProvider`, call `AIAgent.RunAsync()` (MAF handles tools via `FunctionInvokingChatClient`) | Kane | Large |
| `OpenClawNet.Runtime/Orchestrator/AgentOrchestrator.cs` | Resolve `IAgentProvider` instead of `IModelClient` | Kane | Small |
| `OpenClawNet.Gateway/Endpoints/AgentProfileEndpoints.cs` | Add provider-specific fields to response DTOs (Foundry project ID, Copilot model, etc.) | Kane | Small |

#### Code Work (Jobs Domain)

| File/Component | Changes | Owner | PR Size |
|----------------|---------|-------|---------|
| `OpenClawNet.Storage/Entities/ScheduledJob.cs` | **Add columns:** `InputParametersJson`, `LastOutputJson`, `TriggerType` (enum: Manual/Cron/Webhook), `WebhookEndpoint` | Bishop | Small |
| `OpenClawNet.Storage/Entities/JobRun.cs` | **Add columns:** `InputSnapshotJson`, `TokensUsed`, `ExecutedByAgentProfile` | Bishop | Small |
| `OpenClawNet.Storage/Migrations/AddJobsEnhancements.cs` | EF Core migration for new columns | Bishop | Small |
| `OpenClawNet.Runtime/Jobs/JobExecutor.cs` | **New service:** Execute job using MAF Workflow (`WorkflowBuilder` → `InProcessExecution.RunAsync()`) | Kane | Medium |
| `OpenClawNet.Gateway/Endpoints/JobsEndpoints.cs` | **Add 6 endpoints:** `POST /api/jobs/{id}/execute`, `POST /api/jobs/{id}/start`, `POST /api/jobs/{id}/pause`, `POST /api/jobs/{id}/resume`, `POST /api/jobs/{id}/dry-run`, `GET /api/jobs/{id}/stats` | Kane | Large |
| `OpenClawNet.Gateway/DTOs/JobExecutionRequest.cs` | **New DTO:** `{ inputParameters: { key: value } }` for execute/dry-run | Kane | Small |
| `OpenClawNet.Gateway/DTOs/JobStatsResponse.cs` | **New DTO:** `{ totalRuns, totalTokens, avgTokensPerRun, totalCostUsd }` | Kane | Small |

**Total:** ~10 files, 2-3 PRs

#### Tests (Ash)

- **Unit Tests:** `JobExecutor` service (mock `IAgentProvider`, verify MAF Workflow creation)
- **Integration Tests:** 
  - `POST /api/jobs/{id}/execute` → creates JobRun, executes agent, returns result
  - `POST /api/jobs/{id}/dry-run` → executes agent, returns result, does NOT create JobRun
  - `GET /api/jobs/{id}/stats` → aggregates token usage from JobRuns
- **Contract Tests:** Verify MAF Workflow input/output serialization
- **Coverage Target:** 80%+ for Jobs domain

#### Docs (Parker)

- **New:** `/docs/architecture/jobs.md` — Jobs domain model, lifecycle states, execution patterns, MAF Workflow mapping
- **New:** `/docs/architecture/maf-integration.md` — MAF runtime architecture, `IAgentProvider` pattern, Workflow orchestration
- **Update:** `/docs/sessions/session-4/guide.md` — Add Jobs UI + execution walkthrough (Prompts 7-9: Create Job, Execute Job, View JobRun)
- **Update:** `README.md` — Add "Jobs & Scheduling" feature section

#### Aspire/Infra (Bishop)

- **Telemetry:** Add GenAI OTel spans for Job execution (track `job.id`, `job.name`, `agent.profile`, `tokens.used`, `duration.ms`)
- **AppHost:** Consider separating scheduler as standalone resource (optional — can defer to Phase 3)

#### Exit Criteria

- [ ] `DefaultAgentRuntime` uses MAF `AIAgent.RunAsync()` (no manual tool loop)
- [ ] All 6 new Job endpoints functional
- [ ] Dry-run endpoint tested (does NOT persist JobRun)
- [ ] Stats endpoint returns accurate token aggregates
- [ ] All integration tests pass
- [ ] Session 4 guide updated with Jobs walkthrough

---

### Phase 3: Jobs UI + Cleanup (Session 4) — 2-3 days

**Goal:** Deliver Blazor UI for Jobs CRUD + execution. Remove legacy `IModelClient` code.

#### Code Work (Jobs UI)

| File/Component | Changes | Owner | PR Size |
|----------------|---------|-------|---------|
| `OpenClawNet.Web/Pages/Jobs/JobsList.razor` | **New page:** List all jobs with status badges, Quick Actions (Execute, Pause, Resume, Delete) | Kane | Large |
| `OpenClawNet.Web/Pages/Jobs/CreateJob.razor` | **New page:** Multi-step wizard (Name + Prompt → Agent Profile → Schedule → Review) | Kane | Large |
| `OpenClawNet.Web/Pages/Jobs/JobDetail.razor` | **New page:** Job config + Run history table (with expand for full output) | Kane | Medium |
| `OpenClawNet.Web/Pages/Jobs/JobRunDetail.razor` | **New page:** Single run detail (input snapshot, output, tokens, duration, logs) | Kane | Medium |
| `OpenClawNet.Web/Services/JobsClient.cs` | **New HTTP client:** Calls Gateway `/api/jobs/*` endpoints | Kane | Medium |
| `OpenClawNet.Web/Shared/NavMenu.razor` | Add "Jobs" navigation link | Kane | Small |

**Total:** ~6 files, 2 PRs

#### Code Work (Legacy Cleanup)

| File/Component | Changes | Owner | PR Size |
|----------------|---------|-------|---------|
| `OpenClawNet.Runtime/Abstractions/IModelClient.cs` | **Delete** | Kane | Small |
| `OpenClawNet.Runtime/RuntimeModelClient.cs` | **Delete** | Kane | Small |
| `OpenClawNet.Runtime/Adapters/ModelClientChatClientAdapter.cs` | **Delete** | Kane | Small |
| `OpenClawNet.Runtime/DTOs/*` | **Delete:** `ChatMessage`, `ChatResponse`, `ChatRequest`, `ChatResponseChunk`, `ToolDefinition`, `ToolCall`, `UsageInfo` (replaced by `Microsoft.Extensions.AI` types) | Kane | Medium |
| Session guides (all 4) | Replace "IModelClient" references with "IAgentProvider" + MAF terminology | Parker | Medium |

**Total:** ~8 files, 1 PR (cleanup)

#### Tests (Ash)

- **E2E Tests (Kane/Playwright):** 
  - Create Job via UI wizard → verify saved to database
  - Execute Job → verify JobRun appears in detail page
  - Dry-run Job → verify NO JobRun created
  - View Job stats → verify token aggregation displayed
- **Coverage Target:** 4 core Job workflows E2E tested

#### Docs (Parker)

- **New:** `/docs/architecture/skills.md` — Skills strategy (markdown as `AIContextProvider`, future AIFunction SDK for power users)
- **Update:** `/docs/sessions/session-4/guide.md` — Finalize Jobs UI walkthrough with screenshots
- **Update:** `README.md` — Add Jobs UI screenshots to feature gallery

#### Aspire/Infra (Bishop)

- **Scheduler Resource:** If separated from Gateway, add `AddScheduler()` to AppHost (optional — can run as BackgroundService in Gateway)
- **Dashboard:** Verify Jobs telemetry visible in Aspire dashboard (GenAI OTel spans)

#### Exit Criteria

- [ ] Jobs UI fully functional (List, Create, Detail, RunDetail pages)
- [ ] All legacy `IModelClient` code removed
- [ ] E2E tests pass for Jobs workflows
- [ ] Session 4 guide finalized with Jobs content
- [ ] Documentation complete (`jobs.md`, `skills.md`, `maf-integration.md`)

---

### Post-Session-4 Backlog ("Advanced Jobs" Bucket)

These features are deferred based on resolved decisions. Track in GitHub Issues.

#### Event-Driven Job Chaining (Decision #2)

- **Estimate:** 2-3 days
- **Scope:** 
  - Event bus infrastructure (simple in-memory → upgrade to Azure Event Grid later)
  - `JobCompletedEvent` emitted on successful JobRun
  - Subscription model: Job B subscribes to Job A completion
  - API: `POST /api/jobs/{id}/subscriptions` to manage chaining
- **Tests:** Integration tests for event emission + subscription triggers
- **Docs:** `/docs/architecture/job-chaining.md`

#### DAG Workflow Jobs (Decision #1)

- **Estimate:** 1-2 weeks
- **Scope:**
  - MAF multi-executor Workflows (conditional routing, fan-out, sub-workflows)
  - Job definition format: YAML/JSON workflow schema
  - UI: Visual DAG editor (drag-drop agents, connect edges)
  - Examples: Multi-agent triage pipeline, document processing pipeline
- **Tests:** Workflow execution tests for conditionals, parallelism, error handling
- **Docs:** `/docs/architecture/advanced-jobs.md`, `/docs/demos/real-world/`

#### Power-User Skill SDK (Decision #3)

- **Estimate:** 3-4 days
- **Scope:**
  - NuGet package: `OpenClawNet.SkillSDK`
  - `AIFunctionFactory.Create()` helpers for typed C# skills
  - Skill registration API: `POST /api/skills` (upload .dll or NuGet package)
  - Examples: `DotNetAssistantSkill`, `AzureResourceQuerySkill`
- **Tests:** Unit tests for skill registration + invocation
- **Docs:** `/docs/skills/power-user-sdk.md`

#### Job Versioning v2 (Decision #5)

- **Estimate:** 3-4 days
- **Scope:**
  - Snapshot AgentProfile config on Job creation → `Job.ProfileSnapshotJson`
  - UI toggle: "Use latest profile" vs "Use snapshot from creation"
  - Migration: backfill snapshots for existing Jobs
- **Tests:** Integration tests for late-binding vs snapshot resolution
- **Docs:** `/docs/architecture/job-versioning.md`

#### Additional Enhancements

- **Scheduled Trigger UI:** Cron expression builder (visual) instead of raw text input — 1-2 days
- **Webhook Triggers:** Generate webhook URL, validate HMAC signatures — 2-3 days
- **Job Templates:** Pre-built Job definitions (Document Summarizer, Alert Triage, etc.) — 1-2 days
- **Cost Tracking:** Currency conversion for token costs (Azure OpenAI vs Ollama) — 4 hours

---

## Migration Sequencing

### Session 3 Scope

**Deliverable:** Agent Providers + Runtime Cutover

- Phase 1 complete: `IAgentProvider` + 5 providers implemented
- Phase 2 (partial): Runtime cutover to MAF (no Jobs UI yet)
- Tests: Unit + integration for providers
- Docs: `agent-providers.md`

**Exit Criteria:** Existing chat UI works with new MAF runtime (zero breaking changes to user experience)

### Session 4 Scope

**Deliverable:** Jobs CRUD + Execution UI

- Phase 2 (complete): Jobs domain model + API endpoints
- Phase 3: Jobs UI (List, Create, Detail, RunDetail)
- Tests: E2E for Jobs workflows
- Docs: `jobs.md`, `maf-integration.md`, `skills.md`, Session 4 guide updates

**Exit Criteria:** Attendees can create, execute, and monitor Jobs via Blazor UI

### Post-Session-4

**Deliverable:** Advanced Jobs features (backlog)

- Event-driven chaining
- DAG workflows
- Power-user Skill SDK
- Job versioning v2
- Scheduled/webhook triggers enhancements

**Exit Criteria:** Production-ready automation platform

---

## Rollout Checklist

Use this numbered checklist to track PR-by-PR progress. Each item has preconditions, files touched, tests, docs, and exit criteria.

### PR #1: IAgentProvider Interface + Ollama Provider

**Precondition:** None (new code, no dependencies)

**Files Touched:**
- `OpenClawNet.Runtime/Abstractions/IAgentProvider.cs` (new)
- `OpenClawNet.Runtime/Providers/OllamaAgentProvider.cs` (new)
- `OpenClawNet.Runtime.Tests/Providers/OllamaAgentProviderTests.cs` (new)

**Tests Added:**
- Unit: `OllamaAgentProvider.CreateAgent_ReturnsValidAIAgent`
- Unit: `OllamaAgentProvider.IsAvailableAsync_WhenOllamaRunning_ReturnsTrue`

**Docs Updated:**
- `/docs/architecture/agent-providers.md` (new, draft)

**Exit Criteria:**
- [ ] Interface compiled
- [ ] Ollama provider creates `AIAgent` successfully (mocked test)
- [ ] CI passes

---

### PR #2: Azure OpenAI + Foundry Providers

**Precondition:** PR #1 merged

**Files Touched:**
- `OpenClawNet.Runtime/Providers/AzureOpenAIAgentProvider.cs` (new)
- `OpenClawNet.Runtime/Providers/FoundryAgentProvider.cs` (new)
- `OpenClawNet.Runtime.Tests/Providers/AzureOpenAIAgentProviderTests.cs` (new)
- `OpenClawNet.Runtime.Tests/Providers/FoundryAgentProviderTests.cs` (new)
- `OpenClawNet.Storage/Entities/AgentProfile.cs` (add `FoundryProjectId`, `FoundryDeploymentName`)
- `OpenClawNet.Storage/Migrations/AddFoundryFields.cs` (new)

**Tests Added:**
- Unit: Azure OpenAI provider tests (2)
- Unit: Foundry provider tests (2)
- Integration: Migration applied successfully

**Docs Updated:**
- `/docs/architecture/agent-providers.md` (add Azure + Foundry sections)

**Exit Criteria:**
- [ ] Both providers create `AIAgent` successfully
- [ ] AgentProfile entity supports Foundry fields
- [ ] Migration applied without errors

---

### PR #3: Foundry Local Bridge + GitHub Copilot Provider

**Precondition:** PR #2 merged

**Files Touched:**
- `OpenClawNet.Runtime/Providers/FoundryLocalAgentProvider.cs` (new — wraps existing `FoundryLocalModelClient`)
- `OpenClawNet.Runtime/Providers/GitHubCopilotAgentProvider.cs` (new)
- `OpenClawNet.Runtime.Tests/Providers/FoundryLocalAgentProviderTests.cs` (new)
- `OpenClawNet.Runtime.Tests/Providers/GitHubCopilotAgentProviderTests.cs` (new)
- `OpenClawNet.Storage/Entities/AgentProfile.cs` (add `CopilotModel`)

**Tests Added:**
- Unit: Foundry Local bridge tests (2)
- Unit: GitHub Copilot provider tests (2)

**Docs Updated:**
- `/docs/architecture/agent-providers.md` (add Foundry Local + Copilot sections)
- `README.md` (add "Supported Providers" section)

**Exit Criteria:**
- [ ] Foundry Local bridge works (wraps existing client)
- [ ] GitHub Copilot provider creates `AIAgent` (requires local CLI auth)
- [ ] README updated with provider list

---

### PR #4: RuntimeAgentProvider Gateway + DI Registration

**Precondition:** PR #3 merged

**Files Touched:**
- `OpenClawNet.Runtime/RuntimeAgentProvider.cs` (new — router logic)
- `OpenClawNet.Gateway/DependencyInjection.cs` (register all 5 providers + router)
- `OpenClawNet.Runtime.Tests/RuntimeAgentProviderTests.cs` (new — routing tests)

**Tests Added:**
- Integration: `RuntimeAgentProvider` routes to correct provider based on `AgentProfile.ProviderType` (5 tests)

**Docs Updated:**
- `/docs/architecture/agent-providers.md` (finalize with routing section)

**Exit Criteria:**
- [ ] Router selects correct provider for each ProviderType
- [ ] All providers registered in DI
- [ ] Phase 1 complete ✅

---

### PR #5: MAF Runtime Cutover

**Precondition:** PR #4 merged

**Files Touched:**
- `OpenClawNet.Runtime/DefaultAgentRuntime.cs` (replace `IModelClient` with `IAgentProvider`, remove tool loop)
- `OpenClawNet.Runtime/Orchestrator/AgentOrchestrator.cs` (resolve `IAgentProvider`)
- `OpenClawNet.Gateway/Endpoints/AgentProfileEndpoints.cs` (add provider fields to DTOs)

**Tests Added:**
- Integration: Chat endpoint with MAF runtime (existing test should pass)
- Integration: Agent execution with tools (verify MAF `FunctionInvokingChatClient` works)

**Docs Updated:**
- `/docs/architecture/maf-integration.md` (new)
- `/docs/sessions/session-3/guide.md` (optional: add "Under the Hood" section on MAF)

**Exit Criteria:**
- [ ] Chat UI works with new runtime (zero user-facing changes)
- [ ] Tool calling works (MAF handles loop internally)
- [ ] All existing tests pass
- [ ] Phase 2 (runtime) complete ✅

---

### PR #6: Jobs Domain Model + Migrations

**Precondition:** PR #5 merged

**Files Touched:**
- `OpenClawNet.Storage/Entities/ScheduledJob.cs` (add `InputParametersJson`, `LastOutputJson`, `TriggerType`, `WebhookEndpoint`)
- `OpenClawNet.Storage/Entities/JobRun.cs` (add `InputSnapshotJson`, `TokensUsed`, `ExecutedByAgentProfile`)
- `OpenClawNet.Storage/Migrations/AddJobsEnhancements.cs` (new)

**Tests Added:**
- Unit: Entity validation (new columns nullable/required as expected)
- Integration: Migration applied successfully

**Docs Updated:**
- `/docs/architecture/jobs.md` (new — domain model section only)

**Exit Criteria:**
- [ ] Migrations applied without errors
- [ ] Existing Jobs data preserved
- [ ] New columns queryable

---

### PR #7: JobExecutor Service + Execution Endpoints

**Precondition:** PR #6 merged

**Files Touched:**
- `OpenClawNet.Runtime/Jobs/JobExecutor.cs` (new — MAF Workflow execution)
- `OpenClawNet.Gateway/Endpoints/JobsEndpoints.cs` (add `execute`, `start`, `pause`, `resume` endpoints)
- `OpenClawNet.Gateway/DTOs/JobExecutionRequest.cs` (new)
- `OpenClawNet.Runtime.Tests/Jobs/JobExecutorTests.cs` (new)
- `OpenClawNet.Gateway.Tests/Endpoints/JobsEndpointsTests.cs` (new)

**Tests Added:**
- Unit: `JobExecutor.ExecuteJobAsync` creates MAF Workflow, returns result (mocked)
- Integration: `POST /api/jobs/{id}/execute` creates JobRun, persists result

**Docs Updated:**
- `/docs/architecture/jobs.md` (add execution section)

**Exit Criteria:**
- [ ] Job execution works end-to-end (create Job → execute → JobRun saved)
- [ ] JobRun records input snapshot, output, tokens, duration

---

### PR #8: Dry-Run + Stats Endpoints

**Precondition:** PR #7 merged

**Files Touched:**
- `OpenClawNet.Gateway/Endpoints/JobsEndpoints.cs` (add `dry-run`, `stats` endpoints)
- `OpenClawNet.Gateway/DTOs/JobStatsResponse.cs` (new)
- `OpenClawNet.Gateway.Tests/Endpoints/JobsDryRunTests.cs` (new)

**Tests Added:**
- Integration: `POST /api/jobs/{id}/dry-run` executes, returns result, does NOT create JobRun
- Integration: `GET /api/jobs/{id}/stats` aggregates tokens correctly

**Docs Updated:**
- `/docs/architecture/jobs.md` (add dry-run + stats sections)
- `/docs/sessions/session-4/guide.md` (add dry-run testing best practice)

**Exit Criteria:**
- [ ] Dry-run executes without persisting JobRun
- [ ] Stats endpoint returns accurate aggregates
- [ ] Phase 2 (Jobs API) complete ✅

---

### PR #9: Jobs UI — List + Create Pages

**Precondition:** PR #8 merged

**Files Touched:**
- `OpenClawNet.Web/Pages/Jobs/JobsList.razor` (new)
- `OpenClawNet.Web/Pages/Jobs/CreateJob.razor` (new)
- `OpenClawNet.Web/Services/JobsClient.cs` (new)
- `OpenClawNet.Web/Shared/NavMenu.razor` (add Jobs link)

**Tests Added:**
- E2E: Navigate to Jobs page → see list of jobs
- E2E: Click "Create Job" → fill wizard → job saved

**Docs Updated:**
- `/docs/sessions/session-4/guide.md` (add Jobs UI walkthrough, Prompts 7-8)

**Exit Criteria:**
- [ ] Jobs list page displays all jobs with status badges
- [ ] Create Job wizard saves to database
- [ ] Navigation link visible in menu

---

### PR #10: Jobs UI — Detail + RunDetail Pages

**Precondition:** PR #9 merged

**Files Touched:**
- `OpenClawNet.Web/Pages/Jobs/JobDetail.razor` (new)
- `OpenClawNet.Web/Pages/Jobs/JobRunDetail.razor` (new)

**Tests Added:**
- E2E: Click job in list → see detail page with run history
- E2E: Click run in history → see full input/output/tokens

**Docs Updated:**
- `/docs/sessions/session-4/guide.md` (add Job Detail + RunDetail walkthrough, Prompt 9)

**Exit Criteria:**
- [ ] Job detail shows config + run history table
- [ ] Run detail shows input snapshot, output, tokens, duration
- [ ] Phase 3 (Jobs UI) complete ✅

---

### PR #11: Legacy Code Cleanup

**Precondition:** PR #10 merged

**Files Touched:**
- Delete: `IModelClient.cs`, `RuntimeModelClient.cs`, `ModelClientChatClientAdapter.cs`, `DTOs/*`
- Update: All session guides (replace IModelClient with IAgentProvider terminology)

**Tests Added:**
- None (cleanup PR)

**Docs Updated:**
- `/docs/sessions/session-1/guide.md` through `/docs/sessions/session-4/guide.md` (terminology updates)
- `README.md` (remove IModelClient references)

**Exit Criteria:**
- [ ] No references to deleted types remain
- [ ] All tests pass
- [ ] Documentation consistent with MAF terminology
- [ ] Phase 3 (Cleanup) complete ✅

---

### PR #12: Telemetry + Documentation Finalization

**Precondition:** PR #11 merged

**Files Touched:**
- `OpenClawNet.Runtime/Jobs/JobExecutor.cs` (add GenAI OTel spans)
- `OpenClawNet.AppHost/Program.cs` (verify Jobs telemetry visible in dashboard)
- `/docs/architecture/skills.md` (new)

**Tests Added:**
- Integration: Verify telemetry spans emitted for Job execution

**Docs Updated:**
- `/docs/architecture/skills.md` (new — Skills strategy, markdown vs AIFunction)
- `README.md` (add Jobs UI screenshots)

**Exit Criteria:**
- [ ] Job execution traces visible in Aspire dashboard
- [ ] All architecture docs complete (`jobs.md`, `skills.md`, `maf-integration.md`, `agent-providers.md`)
- [ ] Session 4 guide finalized
- [ ] **Full implementation complete ✅**

---

## Backward Compatibility

### Current State (pre-MAF cutover)

OpenClawNet is **already 80% MAF-based**:

- ✅ Runtime uses `ChatClientAgent` from `Microsoft.Agents.AI` (v1.1.0)
- ✅ Skills injected via `AgentSkillsProvider` (MAF's built-in system message enrichment)
- ✅ Tools wrapped as `AIFunction` via `ToolAIFunction` bridge
- ✅ Chat messages use `Microsoft.Extensions.AI.ChatMessage` types

**What's custom:**
- ❌ `IModelClient` abstraction wraps provider-specific clients (Ollama, Azure OpenAI, Foundry Local)
- ❌ `RuntimeModelClient` gateway router selects provider
- ❌ `ModelClientChatClientAdapter` bridges `IModelClient` → `IChatClient`
- ❌ Manual tool-calling loop in `DefaultAgentRuntime` (10 iterations, timeout logic)
- ❌ Custom DTOs for requests/responses (duplicate `Microsoft.Extensions.AI` types)

### Delta During Migration

**Phase 1 (No Breaking Changes):**
- New `IAgentProvider` interface + 5 implementations added
- Old `IModelClient` runtime continues working
- Both coexist in DI

**Phase 2 (Cutover):**
- `DefaultAgentRuntime` switches from `IModelClient` to `IAgentProvider`
- Manual tool loop removed (MAF handles via `FunctionInvokingChatClient`)
- **User-facing impact:** NONE — chat UI behavior identical

**Phase 3 (Cleanup):**
- Delete `IModelClient`, `RuntimeModelClient`, custom DTOs
- **User-facing impact:** NONE — session guides updated for clarity

### What Stays Available

- ✅ **Agent Profiles:** Same config model, same UI, same API
- ✅ **Tools:** Existing tool registry, same `ITool` interface
- ✅ **Skills:** Markdown files continue working as `AIContextProvider`
- ✅ **Chat UI:** SignalR streaming, conversation history, all unchanged
- ✅ **Scheduler:** `ScheduledJob` entity + BackgroundService unchanged (until Jobs API added)
- ✅ **Ollama/Azure OpenAI/Foundry:** All providers continue working (via new `IAgentProvider` implementations)

### What Breaks (Intentionally)

**Nothing breaks for end users.** 

For **developers** extending OpenClawNet:
- ❌ Custom `IModelClient` implementations will need migration to `IAgentProvider`
- ❌ Direct references to `RuntimeModelClient` will need update to `RuntimeAgentProvider`
- ❌ Code using custom DTOs (`ChatMessage`, `ToolCall`, etc.) will need update to `Microsoft.Extensions.AI` types

**Mitigation:** Add migration guide at `/docs/migration/imodelclient-to-iagentprovider.md` (post-Phase-3).

---

## Risks & Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| MAF API changes (1.x instability) | High (breaking changes in runtime) | Low (1.0 GA shipped) | Pin to stable NuGet version (1.1.0); monitor release notes; test upgrades in separate branch |
| Foundry Local .NET support never ships | Medium (bridge becomes permanent) | Medium | Document bridge as "OpenClawNet extension"; consider upstreaming to MAF as community contribution |
| Workflow complexity overwhelming for Session 4 | Medium (attendees confused) | Low | Start simple (single-agent jobs only); defer DAGs to advanced track; provide templates |
| Token cost for Job testing | Low (demo budget overrun) | Medium | Implement dry-run endpoint; default to Ollama (free) for demos; document cost-saving practices |
| Jobs UI performance (100+ JobRuns) | Medium (slow page load) | Low | Paginate run history; index `JobRun.ScheduledJobId` + `JobRun.CreatedAt`; add filtering |
| Migration breaks existing deployments | High (user data loss) | Very Low | Test migrations on copy of production DB; add rollback scripts; version migrations |

---

**End of Document**

