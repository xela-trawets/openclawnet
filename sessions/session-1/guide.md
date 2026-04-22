# Session 1: Foundation + Local Chat

**Duration:** 75-90 minutes | **Level:** Intermediate .NET | **Series:** OpenClawNet — Microsoft Reactor

## Overview

This session introduces the OpenClawNet platform architecture and walks through every layer needed to deliver a working AI chatbot — from the model abstraction interface down to real-time token streaming in the browser via HTTP NDJSON. By the end, attendees will have seen the complete vertical slice: a Blazor UI talking to a Gateway's HTTP streaming endpoint, which calls a local LLM-powered model client, with conversations persisted in SQLite via EF Core, all orchestrated by Aspire.

The session follows an **Explain → Explore → Extend** approach. For each stage, we first explain the concepts and design decisions, then explore the actual code together, and finally extend the code with a small Copilot-assisted modification. This keeps the session interactive without requiring attendees to write large amounts of code from scratch.

OpenClawNet is a pre-built platform with 27 projects and ~4,300 lines of code. The goal is not to live-code everything — the code is already written and working. Instead, we use the session to understand *why* each piece exists and *how* the layers connect. The three Copilot moments are small, focused completions that reinforce understanding of the code patterns.

---

## Stage 1: Architecture & Core Abstractions (15 min)

### Concepts to Explain

- **Vertical-slice architecture**: 27 focused projects organized by responsibility — models, tools, storage, skills, memory, agent runtime, and infrastructure. Each project has a single concern and communicates through interfaces.
- **The `IAgentProvider` contract**: The central abstraction that makes model providers pluggable.Any LLM provider (Ollama, Azure OpenAI, Microsoft Foundry, Foundry Local, GitHub Copilot SDK) implements this single interface via the Microsoft Agent Framework (MAF). This is the key design decision that avoids vendor lock-in.
- **Immutable records for DTOs**: `ChatRequest`, `ChatResponse`, and `ChatMessage` are C# records — immutable, value-equal, and perfect for data transfer. Explain why records over classes for this use case.

### Code Walkthrough

**Project: `OpenClawNet.Models.Abstractions` (93 LOC)**

Walk through the core interface and its supporting types:

```csharp
// IAgentProvider.cs — The contract every provider implements
public interface IAgentProvider
{
    string ProviderName { get; }
    IChatClient CreateChatClient(AgentProfile profile);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
```

Key points to highlight:
- `CreateChatClient` returns an `IChatClient` instance configured with the agent profile — this is the Microsoft Agent Framework (MAF) integration point
- `IsAvailableAsync` enables health checks — critical for Aspire service discovery
- The `IChatClient` interface from MAF handles both streaming and non-streaming completions — production-ready from day one

Then show the data records:

```csharp
// ChatRequest — What we send to the model
public sealed record ChatRequest
{
    public string? Model { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public IReadOnlyList<ToolDefinition>? Tools { get; init; }
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
}

// ChatResponse — What the model sends back
public sealed record ChatResponse
{
    public required string Content { get; init; }
    public required ChatMessageRole Role { get; init; }
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
    public required string Model { get; init; }
    public UsageInfo? Usage { get; init; }
    public string? FinishReason { get; init; }
}
```

Show how DI wires the implementation in `Gateway/Program.cs`:

```csharp
builder.Services.AddSingleton<IAgentProvider, OllamaAgentProvider>();
builder.Services.Configure<ModelOptions>(builder.Configuration.GetSection("Model"));
```

### Live Demo

1. Open the solution in VS Code
2. Show the Solution Explorer with all 27 projects
3. Navigate from `IAgentProvider` → `OllamaAgentProvider` → `ChatStreamEndpoints` to show the dependency chain
4. Open the `solution-structure.svg` diagram to visualize the project organization

### 🤖 Copilot Moment: Implement a New Model Provider

**What:** Given `IAgentProvider` and `OllamaAgentProvider`, ask Copilot to generate `FoundryLocalAgentProvider` from scratch.

**How:**
1. Open `IAgentProvider.cs` and `OllamaAgentProvider.cs` side by side
2. Open Copilot Chat (Ctrl+Shift+I)
3. Reference both files with `#IAgentProvider.cs` and `#OllamaAgentProvider.cs`
4. Type: *"Using OllamaAgentProvider as a reference pattern, implement FoundryLocalAgentProvider using Microsoft.AI.Foundry.Local — match the CreateChatClient method signature from IAgentProvider"*
5. Copilot generates a complete, working implementation in seconds
6. Briefly review: notice it correctly uses the FoundryLocal SDK API, properly implements the `CreateChatClient` method

**Expected output:** A complete `FoundryLocalAgentProvider.cs` class (~100 LOC) that properly implements the `IAgentProvider` interface.

**Teaching point:** This is what clean interface design enables — Copilot sees the contract + one concrete example and extrapolates a working implementation. When your code has good structure, AI amplifies it. The `IAgentProvider` interface is simple but powerful — it's the MAF integration point that makes all providers interchangeable.

---

## Stage 2: Local LLM Provider + Data Layer (15 min)

### Concepts to Explain

- **Local LLMs?** Runtimes like Ollama, Azure OpenAI, or GitHub Copilot SDK that expose models through REST APIs. Options range from local (no API keys, no cost) to cloud-based (managed, scalable).
- **HTTP NDJSON streaming**: The Gateway exposes `POST /api/chat/stream`, which accepts a ChatStreamRequest and returns newline-delimited JSON (NDJSON). Each line is a discrete JSON event (`content`, `complete`, `error`, `tool_start`, etc.) that the client parses incrementally. Errors surface as HTTP status codes — simpler and more reliable than WebSocket-based approaches.
- **EF Core code-first**: The storage layer uses Entity Framework Core with SQLite. Entities are defined as C# classes, and the database schema is derived from them. No SQL, no migrations needed for dev — `EnsureCreatedAsync()` handles it.
- **Entity design decisions**: Soft deletes via timestamps, `OrderIndex` for message sequencing, nullable `ToolCallsJson` for structured tool data, and navigation properties for related data.

### Code Walkthrough

**Project: `OpenClawNet.Models.Ollama` (181 LOC)**

Walk through the provider implementation:

```csharp
// OllamaAgentProvider.cs — CreateChatClient method
public IChatClient CreateChatClient(AgentProfile profile)
{
    // 1. Configure Ollama endpoint and model from profile
    // 2. Return IChatClient instance that wraps OllamaSharp
    // 3. The IChatClient handles streaming via IAsyncEnumerable
    // 4. Yields tokens as they arrive from the LLM
}
```

Key points:
- The provider creates an `IChatClient` configured for the specific model and endpoint
- Streaming is handled through the MAF `IChatClient` interface
- The underlying implementation uses OllamaSharp for NDJSON streaming over HTTP
- Tokens flow to the caller as they arrive — no buffering

**Project: `OpenClawNet.Storage` (275 LOC)**

Walk through the entity model:

```csharp
// Key entities in the storage layer
public sealed class ChatSession        // Conversation container
public sealed class ChatMessageEntity  // Individual messages with role + ordering
public sealed class SessionSummary     // Memory system summaries
public sealed class ToolCallRecord     // Tool execution audit trail
public sealed class ScheduledJob       // Recurring job definitions
public sealed class JobRun             // Job execution history
public sealed class ProviderSetting    // Per-provider configuration
```

Then show `ConversationStore` — the repository pattern:

```csharp
public sealed class ConversationStore : IConversationStore
{
    Task<ChatSession> CreateSessionAsync(string? title = null, ...);
    Task<ChatSession?> GetSessionAsync(Guid sessionId, ...);
    Task<IReadOnlyList<ChatSession>> ListSessionsAsync(...);
    Task DeleteSessionAsync(Guid sessionId, ...);
    Task<ChatMessageEntity> AddMessageAsync(Guid sessionId, string role, string content, ...);
    Task<IReadOnlyList<ChatMessageEntity>> GetMessagesAsync(Guid sessionId, ...);
}
```

### Live Demo

1. Verify Ollama is running: `ollama list` (should show `llama3.2`)
2. Quick curl test: `curl http://localhost:11434/api/tags`
3. Show the entity relationship diagram (`entity-relationship.svg`)
4. Walk through the `OpenClawNetDbContext` configuration

### 🤖 Copilot Moment: New Repository Method

**What:** Add a `GetRecentSessionsAsync` method to `ConversationStore`.

**How:**
1. Open `ConversationStore.cs`
2. Position cursor after the last method
3. Start typing the method signature:

```csharp
public async Task<List<ChatSession>> GetRecentSessionsAsync(int count = 10)
```

4. Let Copilot complete the implementation via inline suggestion (Tab to accept)

**Expected output:**
```csharp
public async Task<List<ChatSession>> GetRecentSessionsAsync(int count = 10)
{
    return await _context.ChatSessions
        .OrderByDescending(s => s.UpdatedAt)
        .Take(count)
        .ToListAsync();
}
```

**Teaching point:** Copilot generates correct LINQ queries from method signatures. The name `GetRecentSessions` + the parameter `count` + the return type `List<ChatSession>` give it enough context to produce `OrderByDescending` + `Take` — the exact pattern you'd write yourself. This demonstrates how well-named methods guide both humans and AI.

---

## Stage 3: Gateway + HTTP NDJSON + Blazor (15 min)

### Concepts to Explain

- **Minimal API pattern**: ASP.NET Core Minimal APIs replace controllers with lambda-based endpoint mapping. Endpoints are organized into static extension classes (`ChatEndpoints`, `SessionEndpoints`) for clean separation.
- **HTTP NDJSON streaming**: The Gateway exposes `POST /api/chat/stream`, returning newline-delimited JSON (NDJSON). Each line is a discrete JSON event (`content`, `complete`, `error`, `tool_start`, etc.) that the client parses incrementally. Errors surface as HTTP status codes — simpler and more reliable than WebSocket-based approaches.
- **Aspire orchestration**: Aspire replaces Docker Compose + manual service wiring. One `AppHost` project defines the topology — which services exist, their dependencies, health checks, and environment variables. `aspire run` starts everything.

### Code Walkthrough

**Project: `OpenClawNet.Gateway` (625 LOC)**

Show the endpoint organization and the new SSE streaming endpoint:

```csharp
// Gateway REST API surface
POST   /api/chat/stream              → Stream chat response as NDJSON
GET    /api/sessions/                 → List all sessions
POST   /api/sessions/                 → Create new session
GET    /api/sessions/{id}             → Get session with messages
DELETE /api/sessions/{id}             → Delete session
PATCH  /api/sessions/{id}/title       → Update session title
GET    /api/settings                  → Get runtime provider settings
PUT    /api/settings                  → Update provider settings
GET    /api/agent-profiles            → List all agent profiles
```

Then walk through `ChatStreamEndpoints` — the core HTTP streaming handler:

```csharp
// ChatStreamEndpoints.cs — HTTP NDJSON streaming
app.MapPost("/api/chat/stream", async (
    ChatStreamRequest request,
    IAgentOrchestrator orchestrator,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    // 1. Validate input
    // 2. Set response headers for NDJSON streaming
    httpContext.Response.ContentType = "application/x-ndjson";
    httpContext.Response.Headers["Cache-Control"] = "no-cache";

    // 3. Stream events from orchestrator as NDJSON
    await foreach (var evt in orchestrator.StreamAsync(agentRequest, cancellationToken))
    {
        var line = JsonSerializer.Serialize(streamEvent, JsonOpts);
        await httpContext.Response.WriteAsync(line + "\n", cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);
    }
})
.WithName("StreamChat")
.WithDescription("Stream a chat response as newline-delimited JSON events");
```

Key points:
- `POST /api/chat/stream` accepts a `ChatStreamRequest` (SessionId, Message, optional Model)
- Response content type is `application/x-ndjson` — each line is a complete JSON object
- Event types include: `"content"` (token delta), `"complete"` (done), `"error"`, `"tool_start"`, `"tool_complete"`
- Errors are caught and sent as JSON events (`ChatStreamEvent` with type `"error"`), making error handling client-side straightforward
- No WebSocket overhead — standard HTTP — making it more resilient and debuggable

Show the DI registration in `Program.cs`:

```csharp
// Gateway/Program.cs — Full DI composition
builder.AddServiceDefaults();          // Aspire telemetry + health
builder.Services.AddOpenClawStorage(); // EF Core + SQLite
builder.Services.AddSingleton<IAgentProvider, OllamaAgentProvider>(); // LLM provider
builder.Services.AddAgentRuntime();    // Orchestrator + prompt composer
builder.Services.AddHostedService<JobSchedulerService>(); // Background jobs
```

**Project: `OpenClawNet.AppHost` (18 LOC)**

```csharp
// AppHost.cs — The entire topology in 18 lines
var gateway = builder.AddProject<Projects.OpenClawNet_Gateway>("gateway")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ConnectionStrings__DefaultConnection", sqliteConnectionString)
    .WithEnvironment("Model__Endpoint", ollamaEndpoint);

builder.AddProject<Projects.OpenClawNet_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(gateway)     // Service discovery
    .WaitFor(gateway)           // Startup ordering
    .WithEnvironment("OpenClawNet__OllamaBaseUrl", ollamaEndpoint);
```

### Live Demo: Provider Switching Without Code Changes

**Demonstration of runtime configuration**: OpenClawNet supports switching LLM providers through the Model Providers page — navigate to Model Providers (`/model-providers`), configure a different provider (Ollama → Azure OpenAI → GitHub Copilot SDK), and send a message to see it work immediately. Model Provider definitions now drive the actual endpoint used for chat: when you save a definition, `ProviderResolver` syncs it to `RuntimeModelSettings`, so the chat flow uses the exact endpoint and credentials from the definition. The active provider and model are shown inline in the chat's agent selector badge.

**Full stack demo:**
1. Run `aspire run` from repo root
2. Open the Aspire Dashboard (`https://localhost:15100`) — show service topology
3. Open the Blazor UI (`http://localhost:5001`)
4. Open the Blazor UI (`http://localhost:5001`)
5. Create a new chat session
6. Send a message: *"What is Aspire and why should I use it?"*
7. Watch tokens stream in real-time — point out the "typing" effect from HTTP NDJSON streaming
8. Open browser DevTools Network tab — show HTTP POST to `/api/chat/stream`, NDJSON response with incremental JSON lines

**Alternative demo approach**: The `bonus-demos.md` file contains a config-file approach to provider switching for those who prefer code-based configuration.

## Closing (5 min)

### Recap: What We Built

Walk through the complete flow one more time:

1. **User types a message** in the Blazor UI
2. **HTTP POST sent** to the Gateway's `/api/chat/stream` endpoint
3. **The orchestrator** composes a prompt and calls `IAgentProvider.CreateChatClient()` to get an `IChatClient`
4. **The IChatClient** (backed by OllamaAgentProvider, Azure OpenAI, or GitHub Copilot SDK) sends a request to the model provider
5. **Tokens stream back** via the MAF streaming interface → NDJSON HTTP response → browser (parsed incrementally)
6. **The conversation is persisted** in SQLite via `ConversationStore`
7. **Aspire orchestrates** the entire startup with health checks and service discovery

### Why Each Layer Matters

- **`IAgentProvider`** — Swap Ollama for Azure OpenAI or GitHub Copilot without changing any other code. The MAF abstraction makes all providers interchangeable.
- **`ConversationStore`** — History, context, and audit trail
- **HTTP NDJSON streaming** — Reliable, debuggable streaming without WebSocket overhead. Errors surface as HTTP status codes.
- **Agent selector badge** — Inline display of active provider and model in the chat UI
- **Aspire** — One command to run, observe, and debug the full stack

### Preview: Session 2

> "We have a chatbot that can answer questions. But what if it could *do* things? In Session 2, we'll give it tools — file system access, web fetching, shell execution — and build the agent loop that decides when and how to use them."

### Resources

- 📦 **GitHub Repository**: [github.com/elbruno/openclawnet](https://github.com/elbruno/openclawnet)
- 📖 **Aspire Documentation**: [learn.microsoft.com/dotnet/aspire](https://learn.microsoft.com/dotnet/aspire)
- 🦙 **Ollama**: [ollama.com](https://ollama.com)
- 🤖 **GitHub Copilot**: [github.com/features/copilot](https://github.com/features/copilot)
- 📡 **HTTP NDJSON**: [developer.mozilla.org/en-US/docs/Web/API/Server-sent_events](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events)
