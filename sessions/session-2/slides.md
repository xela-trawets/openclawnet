---
marp: true
title: "OpenClawNet — Session 2: Tools + Agent Workflows"
theme: default
paginate: true
size: 16:9
---

<!-- _class: lead -->

# OpenClawNet
## Session 2 — Tools & Agent Workflows

**Microsoft Reactor Series · 50 min · Intermediate .NET**

> Turn the **chatbot** from session 1 into an **agent**.

---

## Chatbot vs. agent

| Chatbot | Agent |
|---------|-------|
| Generates text | Generates text **and acts** |
| Pure LLM call | LLM **decides** which tool to call, then we execute it |
| Stateless reply | Loop: prompt → tool calls → results → prompt → … |

> The model never executes anything. **Our code does**, after passing security gates.

---

## What you'll have at the end

- A clean `ITool` abstraction + a thread-safe `ToolRegistry`
- An `IToolExecutor` that **always** runs through an approval gate
- 5 production-ready built-in tools, each defending against a real attack
- A complete agent loop: `prompt → model → tool calls → execute → repeat`
- 3 console demos you can run in 30 seconds each

---

## Today's build, by the numbers

| Slice | LOC | What it gives you |
|-------|-----|-------------------|
| `Tools.Abstractions` | 90 | `ITool`, `IToolExecutor`, `IToolRegistry`, `IToolApprovalPolicy` |
| `Tools.Core` | 101 | `ToolRegistry`, `ToolExecutor`, DI wiring |
| `Tools.FileSystem` | 142 | Reads/writes confined to a workspace |
| `Tools.Shell` | 148 | Cross-platform `cmd`/`sh` with blocklist + 30s timeout |
| `Tools.Web` | 121 | HTTP fetcher with SSRF guard |
| `Tools.Scheduler` | 173 | Cron-based job CRUD |
| `Agent` | — | `DefaultAgentRuntime` — the loop |

---

# 🔧  Stage 1 — Tool Architecture

---

## `ITool` — the contract

```csharp
public interface ITool
{
    string Name { get; }            // unique id, e.g. "file_system"
    string Description { get; }     // shown to the LLM
    ToolMetadata Metadata { get; }  // parameter schema + flags

    Task<ToolResult> ExecuteAsync(
        ToolInput input,
        CancellationToken cancellationToken = default);
}
```

That's it. Implement this and your tool is a first-class citizen.

---

## `ToolMetadata` — what the LLM sees

```csharp
public ToolMetadata Metadata => new()
{
    Name             = Name,
    Description      = Description,
    ParameterSchema  = JsonDocument.Parse("""
        { "type":"object", "properties":{ "path":{"type":"string"} },
          "required":["path"] }
    """),
    RequiresApproval = false,
    Category         = "files",
    Tags             = ["read", "io"]
};
```

The model sees JSON Schema. **It never sees your implementation.**

---

## Registry vs. Executor — separation of concerns

| `IToolRegistry` | `IToolExecutor` |
|-----------------|-----------------|
| Discovery | Safe execution |
| `Register`, `GetTool`, `GetManifest` | `ExecuteAsync`, `ExecuteBatchAsync` |
| Knows **what exists** | Knows **how to run safely** |
| Singleton | Scoped (per request) |

> The Executor is the **chokepoint**. Every tool call passes through the approval gate.

---

## The approval gate, in 6 lines

```csharp
var tool = _registry.GetTool(toolName);
if (tool is null) return ToolResult.Fail(...);

if (await _policy.RequiresApprovalAsync(toolName, args) &&
    !await _policy.IsApprovedAsync(toolName, args))
    return ToolResult.Fail(toolName, "approval required");

var result = await tool.ExecuteAsync(input, ct);
```

Built-in: `AlwaysApprovePolicy`. Production: implement your own (RBAC, MFA, hold-and-confirm).

---

## Wiring it up — one extension method

```csharp
services.AddToolFramework();      // registry + executor + AlwaysApprove
services.AddTool<FileSystemTool>();
services.AddTool<ShellTool>();
services.AddTool<WebTool>();
services.AddTool<SchedulerTool>();
services.AddTool<CalculatorTool>();
```

Add a tool → one line. Remove a tool → one line. **No surgery.**

---

# 🛡️  Stage 2 — Built-in Tools & Security

---

## 3 attacks every tool framework must block

1. **Path traversal** — `../../etc/passwd`, `..\..\Windows\System32`
2. **Command injection** — `rm -rf /`, `format C:`
3. **SSRF** — `http://127.0.0.1:8080/admin`, `http://169.254.169.254/`

> The model is not malicious — it's just **gullible**. Treat its output like untrusted user input.

---

## FileSystemTool — kill traversal at resolution time

```csharp
var fullPath = Path.GetFullPath(
    Path.Combine(_workspaceRoot, relativePath));

if (!fullPath.StartsWith(_workspaceRoot,
        StringComparison.OrdinalIgnoreCase))
{
    _logger.LogWarning("Path traversal blocked: {Path}", relativePath);
    return null;
}
```

`Path.GetFullPath` resolves `..`. By the time we check, the trick is gone.
Plus: **1 MB read cap**, deny-list for `.env` / `.git` / `appsettings.Production`.

---

## ShellTool — blocklist + timeout + approval

```csharp
private static readonly HashSet<string> BlockedCommands =
    new(StringComparer.OrdinalIgnoreCase)
{
    "rm","del","format","fdisk","mkfs","dd",
    "shutdown","reboot","kill","taskkill",
    "net","reg","regedit","powershell","cmd"
};
```

- 30-second hard timeout (`CancellationTokenSource.CreateLinkedTokenSource`)
- Output capped at 10 000 chars
- `RequiresApproval = true` — every invocation hits the gate

---

## WebTool — block SSRF before the socket opens

```csharp
private static bool IsLocalUri(Uri uri) =>
    uri.Host is "localhost" or "127.0.0.1" or "::1"
    || uri.Host.StartsWith("192.168.")
    || uri.Host.StartsWith("10.")
    || uri.Host.StartsWith("172.16.");
```

- Schemes: `http` and `https` **only**
- 50 KB response cap, 15 s timeout
- Production tip: also resolve DNS to catch `evil.com → 127.0.0.1`

---

## SchedulerTool — let the agent schedule its own work

- Three actions: `create`, `list`, `cancel`
- Persists to SQLite via `IDbContextFactory<OpenClawDbContext>`
- One-shot (`ISO 8601 datetime`) or recurring (`cron`)
- Used live in **session 3** for long-running agentic jobs

---

## 🤖 Copilot moment

> "Add `wget` and `curl` to the blocked commands list in ShellTool. These tools could be used to exfiltrate data. Add a comment explaining why network tools are blocked."

A 5-second prompt; a real defense-in-depth improvement.
**Good architecture makes good security cheap.**

---

# 🔄  Stage 3 — The Agent Loop

---

## What an agent loop actually does

```
1. Compose: system + history + user message + tool manifest
2. Call model → get response
3. If response has tool calls → execute each, append results
4. If response has finish_reason "stop" → done
5. Otherwise → goto step 2
```

Cap iterations (`MaxToolIterations = 10`) to prevent runaway loops.

---

## `DefaultAgentRuntime` — the engine

```csharp
public async Task<AgentResponse> ExecuteAsync(AgentContext ctx, CT ct)
{
    for (int i = 0; i < MaxToolIterations; i++)
    {
        var result = await _modelClient.CompleteAsync(request, ct);
        if (result.ToolCalls is { Count: > 0 } calls)
        {
            foreach (var call in calls)
            {
                var tr = await _executor.ExecuteAsync(
                    call.Name, call.Arguments, ct);
                ctx.ExecutedToolCalls.Add(call);
                ctx.ToolResults.Add(tr);
                request.Messages.Add(ToToolResultMessage(call, tr));
            }
            continue;
        }
        return AgentResponse.Final(result.Content);
    }
}
```

---

## How tools get into the prompt

`DefaultPromptComposer.Compose(...)`

```csharp
var manifest = _registry.GetToolManifest(); // metadata only
return new ChatRequest
{
    Messages = [ ..systemPrompt, ..history, userMessage ],
    Tools    = manifest          // names + JSON Schema for the model
};
```

The composer **never** ships your code to the LLM — only names, descriptions, schemas.

---

## Streaming + tools = NDJSON event types

```jsonc
{ "type":"content_delta",       "delta":"Sure, I'll check..." }
{ "type":"tool_call_start",     "tool":"file_system", "id":"c1" }
{ "type":"tool_call_complete",  "id":"c1", "ok":true }
{ "type":"content_delta",       "delta":"Found 3 files." }
{ "type":"complete" }
```

The Web UI renders each event in real time — every tool call is visible.

---

# 🧪  Stage 4 — Demos

---

## Demo 1 — Implement an `ITool`

[`code/demo1-tool/`](./code/demo1-tool/)

A 60-line console: an "echo with rules" tool that demonstrates `Metadata`, `ParameterSchema`, `ToolResult.Ok` / `Fail`.

```pwsh
cd docs\sessions\session-2\code\demo1-tool
dotnet run
```

---

## Demo 2 — The approval gate in action

[`code/demo2-approval/`](./code/demo2-approval/)

Same `ToolExecutor` you ship in production, with two policies:
- `AlwaysApprovePolicy` → ✅ runs
- A custom `DenyDangerousArgsPolicy` → ❌ blocked

Shows that **the gate is one DI swap**, not a code rewrite.

---

## Demo 3 — A tiny agent loop with real tools

[`code/demo3-agent-loop/`](./code/demo3-agent-loop/)

Wires `OllamaAgentProvider` + `Calculator` + `FileSystem` and runs the loop.
Watch the model decide which tool to call.

```pwsh
ollama pull gemma3:4b   # tool-capable model
cd docs\sessions\session-2\code\demo3-agent-loop
dotnet run "What is sqrt(2) * pi, rounded to 4 decimals?"
```

---

## Going further — built-in templates

Open the running app at **/jobs/templates** for 5 ready-to-run agentic workflows that use these tools:

- 📂 Watched-folder summarizer
- 🐙 GitHub issue triage
- 🔎 Research and archive
- 🖼️ Image batch resize
- 🔊 Text-to-speech snippet

Each one is a real `ScheduledJob` you can clone in one click.

---

## Where we go next

- **Session 3** — long-running jobs, run-event timeline, the dashboard
- **Session 4** — MCP servers (in-process + remote)
- **Bonus** — Multi-agent orchestration

---

<!-- _class: lead -->

# Questions?

elbruno/openclawnet · MIT licensed
