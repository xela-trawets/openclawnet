# Session 3: Skills + Memory

**Duration:** 50 minutes | **Level:** Intermediate .NET

## Overview

Same agent, different users — and you want them to see different behavior. In this session we add **skills** (Markdown files with YAML frontmatter that shape agent behavior without code changes) and **memory** (automatic summarization to manage context windows, plus semantic search across past conversations). By the end, the agent is personalized, context-efficient, and remembers what happened last time.

**Builds on:** Session 1 (foundation) and Session 2 (tools + security).

---

## Before the Session

### Prerequisites

- Session 2 complete and working
- Local LLM running (Ollama with `llama3.2` or Foundry Local)
- Understanding of: async/await, file I/O, EF Core basics
- Sample skill files already exist in `skills/built-in/` and `skills/samples/`

### Starting Point

- The `session-2-complete` code
- Tool loop is working
- Agent orchestrator is operational
- Database schema is solid

### Presenter Preparation (10 min before)

1. Run `aspire run` — verify Session 2 works end-to-end
2. Prepare a test skill file for live demo purposes
3. Navigate to Skills page — toggle one skill to confirm loading
4. Prepare conversation history (run 20+ messages to trigger summarization)
5. Have sample skills ready to display on screen

### Git Checkpoint

**Starting tag:** `session-3-start` (alias: `session-2-complete`)
**Ending tag:** `session-3-complete`

---

## Stage 1: Skill System (12 min)

### Concepts

- **What is a skill?** A Markdown file with YAML frontmatter. No code changes needed — drop a file, reload, and the agent behaves differently.
- **FileSkillLoader:** Scans `skills/built-in/` and `skills/samples/` directories for `*.md` files, parses each one, tracks enabled/disabled state.
- **SkillParser:** Extracts YAML frontmatter using regex (`^---\s*\n(.*?)\n---\s*\n(.*)$`), returns metadata + content.
- **DefaultPromptComposer integration:** Active skills are injected into the system prompt as `## Skill: {name}\n{content}`. The agent sees them as instructions.

### Code Walkthrough

#### Sample Skill File (`dotnet-expert.md`)

```markdown
---
name: dotnet-expert
description: .NET development expertise and best practices
tags: [dotnet, csharp, programming, architecture]
enabled: true
---

You are a .NET expert assistant. When answering questions:

- Reference official Microsoft documentation and established patterns
- Prefer modern C# features (records, pattern matching, file-scoped namespaces)
- Recommend Aspire for distributed applications
- Follow the Microsoft coding conventions and naming guidelines
```

**What to explain:** YAML frontmatter fields (`name`, `description`, `tags`, `enabled`). Content after the closing `---` is pure Markdown — the agent's behavior instructions.

#### SkillDefinition and SkillContent Models

```csharp
// SkillDefinition — immutable metadata
public sealed record SkillDefinition(
    string Name,
    string Description,
    string Category,
    string[] Tags,
    bool Enabled,
    string FilePath,
    string[] Examples);

// SkillContent — what gets injected into the prompt
public sealed record SkillContent(
    string Name,
    string Content,
    string Description,
    string[] Tags);
```

**What to explain:** Sealed records for immutability. `SkillDefinition` is for listing/UI. `SkillContent` is what the prompt composer actually uses.

#### FileSkillLoader Implementation

```csharp
public class FileSkillLoader : ISkillLoader
{
    // Scans directories for *.md files
    // Thread-safe with lock for _disabledSkills HashSet
    // Graceful error handling — malformed files are skipped with a warning

    public async Task<IReadOnlyList<SkillContent>> GetActiveSkillsAsync(CancellationToken ct)
    {
        // Returns only skills that are NOT in _disabledSkills
    }

    public void EnableSkill(string name) => _disabledSkills.Remove(name);
    public void DisableSkill(string name) => _disabledSkills.Add(name);
}
```

**What to explain:** Thread-safety via lock. Disable tracking is in-memory (`HashSet<string>`). `ReloadAsync()` re-scans the directory without restart.

#### How Skills Weave into the System Prompt

```csharp
// DefaultPromptComposer.ComposeAsync()
var skills = await _skillLoader.GetActiveSkillsAsync(cancellationToken);
var skillText = string.Join("\n\n", skills.Select(s => $"## Skill: {s.Name}\n{s.Content}"));

var systemContent = DefaultSystemPrompt + $"\n\n# Active Skills\n{skillText}";
```

**What to explain:** The system prompt is built dynamically. Skills appear as Markdown sections the LLM reads as instructions. More skills = longer system prompt = more tokens.

### 🤖 Copilot Moment (~minute 10)

**Goal:** Create a brand-new skill file from scratch.

> See `copilot-prompts.md` → Prompt 1 for the exact prompt.

**Expected outcome:** A complete `security-auditor.md` file with valid YAML frontmatter and behavior instructions. Reload skills and confirm it appears in the skill list.

**How to test:**
```bash
# Reload skills via API
curl -X POST http://localhost:5000/api/skills/reload

# Verify it appears
curl http://localhost:5000/api/skills | jq '.[] | select(.name == "security-auditor")'
```

---

## Stage 2: Memory & Summarization (15 min)

### Concepts

- **Context window problem:** LLMs have token limits (e.g., 8K–128K). Long conversations fill up fast. Sending everything is expensive and eventually fails.
- **Summarization strategy:** Keep recent N messages verbatim. Compress older messages into a summary. Inject summary at the top of the prompt so the agent has context without the full history.
- **Semantic search with local embeddings:** Convert text to vectors using `Elbruno.LocalEmbeddings` (ONNX models, runs locally). Find past conversations by meaning, not just keyword match.

### Code Walkthrough

#### DefaultMemoryService

```csharp
public class DefaultMemoryService : IMemoryService
{
    private readonly IDbContextFactory<OpenClawDbContext> _dbContextFactory;

    public async Task<string?> GetSessionSummaryAsync(Guid sessionId)
    {
        // Returns the most recent summary for a session
    }

    public async Task StoreSummaryAsync(Guid sessionId, string summary, int messageCount)
    {
        // Persists a new SessionSummary entity
    }

    public async Task<MemoryStats> GetStatsAsync(Guid sessionId)
    {
        // Returns TotalMessages, SummaryCount, CoveredMessages, LastSummaryAt
    }
}
```

**What to explain:** Uses `IDbContextFactory` (not a singleton `DbContext`) — correct pattern for async services. `MemoryStats` gives the UI transparency into what the memory system is doing.

#### DefaultEmbeddingsService

```csharp
public class DefaultEmbeddingsService : IEmbeddingsService
{
    // Backed by Elbruno.LocalEmbeddings (ONNX model, runs locally)

    public async Task<float[]> EmbedAsync(string text)
    {
        // Returns embedding vector for a single text
    }

    public float CosineSimilarity(float[] v1, float[] v2)
    {
        // dot product / (magnitude1 * magnitude2)
    }
}
```

**What to explain:** Embeddings are numeric vectors that capture meaning. Cosine similarity measures how "close" two texts are semantically. Running locally means no API calls, no data leaves the machine.

#### SessionSummary Entity in Storage

```csharp
public sealed class SessionSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int CoveredMessageCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ChatSession Session { get; set; } = null!;
}
```

**What to explain:** One session can have many summaries (as the conversation grows). `CoveredMessageCount` tracks how many messages were compressed. Cascade-deletes with the parent session.

### Live Demo

1. **Summarization trigger:** Send 20+ messages in a conversation. Show that older messages get summarized. Call the stats endpoint to see the count.
2. **Semantic search:** Search past conversations by meaning (e.g., "dependency injection" finds discussions about DI even if those exact words weren't used).

```bash
# Check memory stats
curl http://localhost:5000/api/memory/{sessionId}/stats

# Get all summaries
curl http://localhost:5000/api/memory/{sessionId}/summaries
```

---

## Stage 3: Integration + UI (15 min)

### Concepts

- **Skills API:** Enable/disable skills at runtime without restarting the server. Reload to pick up new files.
- **Memory stats:** Transparent to the user — they can see how many messages are summarized, when the last summary happened, token usage.
- **Before/after pattern:** Toggle a skill on → ask a question → get expert response. Toggle it off → same question → generic response.

### Code Walkthrough

#### SkillEndpoints in Gateway

```csharp
public static class SkillEndpoints
{
    public static void MapSkillEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/skills").WithTags("Skills");

        group.MapGet("/", async (ISkillLoader loader) =>
            Results.Ok(await loader.ListSkillsAsync()));

        group.MapPost("/reload", async (ISkillLoader loader) =>
            Results.Ok(new { reloaded = true, count = (await loader.ListSkillsAsync()).Count }));

        group.MapPost("/{name}/enable", (string name, ISkillLoader loader) => {
            loader.EnableSkill(name);
            return Results.Ok(new { name, enabled = true });
        });

        group.MapPost("/{name}/disable", (string name, ISkillLoader loader) => {
            loader.DisableSkill(name);
            return Results.Ok(new { name, enabled = false });
        });
    }
}
```

**What to explain:** Minimal API pattern — each endpoint is a single lambda. `ISkillLoader` is injected by DI. No restart needed for enable/disable.

#### MemoryEndpoints in Gateway

```csharp
public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/memory").WithTags("Memory");

        group.MapGet("/{sessionId:guid}/summary", async (Guid sessionId, IMemoryService memoryService) =>
            Results.Ok(new { sessionId, summary = await memoryService.GetSessionSummaryAsync(sessionId) }));

        group.MapGet("/{sessionId:guid}/summaries", async (Guid sessionId, IMemoryService memoryService) =>
            Results.Ok(await memoryService.GetAllSummariesAsync(sessionId)));

        group.MapGet("/{sessionId:guid}/stats", async (Guid sessionId, IMemoryService memoryService) =>
            Results.Ok(await memoryService.GetStatsAsync(sessionId)));
    }
}
```

**What to explain:** Three read-only endpoints. Stats gives the UI everything it needs to render a memory dashboard. This is the endpoint the Copilot moment will extend.

### Live Demo

1. **Skill toggle — before/after:**
   - Enable `dotnet-expert` → ask: "What's the best way to handle DI in .NET?" → expert response with specific patterns
   - Disable `dotnet-expert` → same question → generic response
2. **Memory stats panel:** Show the Blazor UI memory stats component — total messages, summary count, last summary time.

### 🤖 Copilot Moment (~minute 40)

**Goal:** Add a search-by-date filter to `MemoryEndpoints`.

> See `copilot-prompts.md` → Prompt 2 for the exact prompt.

**Expected outcome:** A new endpoint `GET /api/memory/{sessionId}/summaries?from=...&to=...` that filters summaries by `CreatedAt` range.

**How to test:**
```bash
# Get summaries from the last hour
curl "http://localhost:5000/api/memory/{sessionId}/summaries?from=2025-01-01T00:00:00Z&to=2025-12-31T23:59:59Z"
```

---

## Closing (8 min)

### Key Insight

> **"Skills are just markdown. Memory is transparent."**

Anyone can create a skill — no C# required. Memory management is visible to the user, not a black box.

### What We Built Today (Checklist)

- [x] Skill system: YAML + Markdown files → agent behavior
- [x] FileSkillLoader: scan, parse, enable/disable at runtime
- [x] DefaultPromptComposer: skills woven into system prompt
- [x] DefaultMemoryService: summarization with database persistence
- [x] DefaultEmbeddingsService: local semantic search
- [x] Skills API endpoints (list, enable, disable, reload)
- [x] Memory API endpoints (summary, stats)
- [x] Copilot: created a skill file from scratch
- [x] Copilot: added date filtering to memory endpoints

### Preview Session 4

Our agent has personality and memory. **Next session:** cloud deployment with Azure, CI/CD pipelines, production configuration, and monitoring. We take OpenClawNet from localhost to the real world.

---

## DI Registration Reference

```csharp
// Skills registration
services.AddSingleton<ISkillLoader>(sp =>
    new FileSkillLoader(skillDirectories, sp.GetRequiredService<ILogger<FileSkillLoader>>()));

// Memory registration
services.AddScoped<IMemoryService, DefaultMemoryService>();
services.AddSingleton<IEmbeddingsService, DefaultEmbeddingsService>();
```

## Projects Covered

| Project | LOC | Key Responsibility |
|---------|-----|-------------------|
| OpenClawNet.Skills | 237 | Skill definitions, loading, parsing |
| OpenClawNet.Memory | 234 | Summarization, embeddings, stats |
| OpenClawNet.Agent | — | DefaultPromptComposer (skill integration) |
| OpenClawNet.Storage | — | SessionSummary entity |
| OpenClawNet.Gateway | — | SkillEndpoints, MemoryEndpoints |
