# Session 2: Tools + Agent Workflows

**Duration:** 50 minutes | **Level:** Intermediate .NET

## Overview

In agent architecture, a **tool** is any capability the AI can invoke to interact with the world — reading files, running commands, fetching web pages, scheduling tasks. The difference between a chatbot and an agent is simple: **a chatbot generates text; an agent uses tools**.

This session follows the **"Explain → Explore → Extend"** approach: we explain the architecture, explore the pre-built code, then extend it with small Copilot-assisted changes. Security is a first-class concern — every tool has defense-in-depth protections against real-world attack vectors.

### What Attendees Will Understand

- How the tool abstraction layer enables extensibility
- Why security gates (approval, validation, blocklists) are essential
- How the agent loop coordinates model reasoning with tool execution
- The separation between `IToolRegistry` (what tools exist) and `IToolExecutor` (how tools run safely)

---

## Stage 1: Tool Architecture (12 min)

### Concepts to Explain

- **What makes an agent vs a chatbot**: Tool use! A chatbot generates text. An agent decides it needs to *do something* — read a file, run a command, fetch a URL — and requests a tool call. The model doesn't execute anything; it emits a structured tool call that our code executes.
- **ITool interface**: Every tool implements `ITool` with four members:
  - `Name` — unique identifier (e.g., `"file_system"`, `"shell"`)
  - `Description` — what the tool does (fed to the LLM)
  - `Metadata` — parameter schema, approval requirements, category, tags
  - `ExecuteAsync(ToolInput, CancellationToken)` — the actual execution
- **Approval policies**: `IToolApprovalPolicy` with two methods:
  - `RequiresApprovalAsync` — does this tool need human approval?
  - `IsApprovedAsync` — has it been approved?
  - Built-in: `AlwaysApprovePolicy` (auto-approve everything)
  - ShellTool sets `RequiresApproval = true` in its metadata
- **IToolRegistry and IToolExecutor separation of concerns**:
  - `IToolRegistry` manages tool discovery: `Register`, `GetTool`, `GetAllTools`, `GetToolManifest`
  - `IToolExecutor` manages safe execution: lookup → approval check → execute → log
  - Why separate? Registry is about *what exists*; executor is about *how to run safely*

### Code Walkthrough

#### Tools.Abstractions (7 files, 90 LOC)

Walk through each file briefly:

1. **`ITool.cs`** — The core interface. Every tool in the system implements this. Point out that `ExecuteAsync` returns `ToolResult`, not raw strings.

2. **`IToolExecutor.cs`** — Two methods: `ExecuteAsync` (single tool) and `ExecuteBatchAsync` (multiple tools). The executor doesn't know about specific tools — it uses the registry.

3. **`IToolRegistry.cs`** — Four methods: `Register`, `GetTool`, `GetAllTools`, `GetToolManifest`. The manifest returns metadata only (no execution capability) — safe to expose to the model.

4. **`IToolApprovalPolicy.cs`** — The security gate interface. Includes `AlwaysApprovePolicy` as a default. In production, you'd implement a policy that checks user permissions.

5. **`ToolInput.cs`** — Wraps raw JSON arguments with helper methods: `GetArgument<T>`, `GetStringArgument`. Uses `JsonDocument.Parse` for zero-allocation access.

6. **`ToolMetadata.cs`** — What the LLM sees: Name, Description, `ParameterSchema` (JSON Schema), `RequiresApproval`, Category, Tags.

7. **`ToolResult.cs`** — Success/failure with output, error, and duration. Factory methods: `ToolResult.Ok(...)` and `ToolResult.Fail(...)`.

#### Tools.Core (3 files, 101 LOC)

1. **`ToolExecutor.cs`** — The approval gate pattern:
   ```csharp
   // 1. Lookup
   var tool = _registry.GetTool(toolName);
   if (tool is null) return ToolResult.Fail(...);

   // 2. Approval check
   if (await _approvalPolicy.RequiresApprovalAsync(toolName, arguments) &&
       !await _approvalPolicy.IsApprovedAsync(toolName, arguments))
       return ToolResult.Fail(...);

   // 3. Execute with timing
   var sw = Stopwatch.StartNew();
   var result = await tool.ExecuteAsync(input, cancellationToken);
   ```
   Point out: every execution is logged with duration. The executor is a chokepoint — all tool calls flow through it.

2. **`ToolRegistry.cs`** — Thread-safe dictionary with `StringComparer.OrdinalIgnoreCase`. Simple but effective — tool names are case-insensitive.

3. **`ToolsServiceCollectionExtensions.cs`** — DI wiring:
   - `AddToolFramework()` registers Registry (singleton), Executor (scoped), ApprovalPolicy (singleton)
   - `AddTool<T>()` registers individual tools as singletons

### Live Demo

**Show the tool list endpoint: `GET /api/tools`**

1. Open browser or HTTP client
2. Navigate to `https://localhost:{port}/api/tools`
3. Show the JSON response — list of tool metadata (name, description, parameter schema)
4. Point out: this is what the model sees when deciding which tool to call

---

## Stage 2: Built-in Tools + Security (15 min)

### Concepts to Explain

Three real-world security threats that agent tools must defend against:

1. **Path Traversal** — An attacker (or confused LLM) tries `../../etc/passwd` or `..\..\Windows\System32`. The file system tool must confine access to the workspace.
2. **Command Injection** — The LLM generates `rm -rf /` or `format C:`. The shell tool must block dangerous commands before they execute.
3. **SSRF (Server-Side Request Forgery)** — The LLM fetches `http://127.0.0.1:8080/admin` or `http://169.254.169.254/metadata`. The web tool must block requests to internal/private networks.

**Defense pattern**: Each tool validates inputs *before* execution. Fail fast, fail safe.

### Code Walkthrough

#### FileSystemTool (`OpenClawNet.Tools.FileSystem`, 142 LOC)

Key security features to highlight:

1. **Blocked paths array**:
   ```csharp
   private static readonly string[] BlockedPaths = [".env", ".git", "appsettings.Production"];
   ```

2. **Path resolution with traversal prevention** — the `ResolvePath` method:
   ```csharp
   var fullPath = Path.GetFullPath(Path.Combine(_workspaceRoot, relativePath));
   if (!fullPath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
   {
       _logger.LogWarning("Path traversal attempt blocked: {Path}", relativePath);
       return null;
   }
   ```
   Explain: `Path.GetFullPath` resolves `..` segments. Then we check the result stays inside the workspace. The `..` is gone by the time we check — this catches all traversal tricks.

3. **File size limit**: 1MB maximum to prevent memory exhaustion.

4. **Three operations**: read, write, list — each with appropriate guards.

#### ShellTool (`OpenClawNet.Tools.Shell`, 148 LOC)

Key security features:

1. **Command blocklist**:
   ```csharp
   private static readonly HashSet<string> BlockedCommands = new(StringComparer.OrdinalIgnoreCase)
   {
       "rm", "del", "format", "fdisk", "mkfs", "dd", "shutdown", "reboot",
       "kill", "taskkill", "net", "reg", "regedit", "powershell", "cmd"
   };
   ```

2. **Safety check** — extracts first word, strips path prefix, checks blocklist:
   ```csharp
   private static bool IsSafeCommand(string command)
   {
       var firstWord = command.Split(' ', StringSplitOptions.RemoveEmptyEntries)
           .FirstOrDefault()?.ToLowerInvariant();
       firstWord = Path.GetFileNameWithoutExtension(firstWord);
       return !BlockedCommands.Contains(firstWord);
   }
   ```

3. **Timeout**: 30 seconds max execution with `CancellationTokenSource.CreateLinkedTokenSource` and process tree kill.

4. **Output limit**: 10,000 characters to prevent memory exhaustion.

5. **Cross-platform**: Uses `cmd.exe /c` on Windows, `/bin/sh -c` on Linux/Mac.

6. **RequiresApproval = true**: This tool requires explicit approval (unlike file system and web).

#### WebTool (`OpenClawNet.Tools.Web`, 121 LOC)

Key security features:

1. **SSRF prevention** — the `IsLocalUri` method:
   ```csharp
   private static bool IsLocalUri(Uri uri)
   {
       var host = uri.Host.ToLowerInvariant();
       return host == "localhost" ||
              host == "127.0.0.1" ||
              host == "::1" ||
              host.StartsWith("192.168.") ||
              host.StartsWith("10.") ||
              host.StartsWith("172.16.");
   }
   ```
   Explain: This blocks requests to internal networks. In production, you'd also resolve DNS to catch CNAME tricks (e.g., `evil.com` → `127.0.0.1`).

2. **Scheme validation**: Only `http` and `https` — no `file://`, `ftp://`, `gopher://`.

3. **Response limit**: 50,000 characters to prevent memory exhaustion.

4. **Timeout**: 15 seconds.

#### SchedulerTool (`OpenClawNet.Tools.Scheduler`, 173 LOC)

- Three actions: `create`, `list`, `cancel`
- Database persistence via EF Core (`IDbContextFactory<OpenClawDbContext>`)
- Supports one-time jobs (ISO 8601 datetime) and recurring jobs (cron expressions)
- Lists up to 20 jobs with status and next run time
- Job cancellation by GUID

### 🤖 Copilot Moment: Add a Blocked Command Pattern

**When:** ~minute 22

**Context:** We've just walked through the ShellTool blocklist. Now let's extend it.

**What to do:** Open `ShellTool.cs`, place cursor inside the `BlockedCommands` HashSet, and ask Copilot:

> Add `wget` and `curl` to the blocked commands list in the ShellTool. These tools could be used to exfiltrate data from the server. Also add a comment explaining why network tools are blocked.

**Expected result:** Copilot adds `"wget"` and `"curl"` to the `BlockedCommands` HashSet and adds a comment about data exfiltration prevention.

**Why it's interesting:** Small, focused change that reinforces the security mindset. Shows that extending the defense is trivial with good architecture.

---

## Stage 3: Agent Loop + Integration (15 min)

### Concepts to Explain

- **The agent reasoning loop**: This is the core algorithm that makes an agent an agent:
  1. Compose prompt (system + history + user message + tool definitions)
  2. Send to model
  3. Model responds with either text OR tool calls
  4. If tool calls → execute each tool → add results to conversation → go to step 2
  5. If text → return final response
  
  This loop repeats until the model has no more tool calls, or we hit the safety limit.

- **Max iterations as safety limit**: `MaxToolIterations = 10`. Without this, a confused model could loop forever. After 10 iterations, the agent returns a "max iterations reached" message.

- **How tools get injected into the system prompt**: The `DefaultPromptComposer` builds the full prompt:
  1. System message (base prompt + active skills + session summary)
  2. Conversation history
  3. Current user message
  
  Tool definitions are passed separately to the model API as structured `ToolDefinition` objects — the model sees the name, description, and parameter schema for every registered tool.

### Code Walkthrough

#### AgentOrchestrator (`OpenClawNet.Agent`)

The orchestrator is the public API — it creates an `AgentContext` and delegates to `IAgentRuntime`:

```csharp
public async Task<AgentResponse> ProcessAsync(AgentRequest request, CancellationToken cancellationToken)
{
    var context = new AgentContext
    {
        SessionId = request.SessionId,
        UserMessage = request.UserMessage,
        ModelName = request.Model ?? "llama3.2",
        ProviderName = request.Provider
    };

    var executedContext = await _runtime.ExecuteAsync(context, cancellationToken);

    return new AgentResponse
    {
        Content = executedContext.FinalResponse ?? string.Empty,
        ToolResults = executedContext.ToolResults,
        ToolCallCount = executedContext.ExecutedToolCalls.Count,
        TotalTokens = executedContext.TotalTokens
    };
}
```

Point out: The orchestrator doesn't know about tools, models, or prompts. It's a coordinator.

#### DefaultAgentRuntime — The Core Loop

Walk through the tool-call loop in detail:

```csharp
while (iterations < MaxToolIterations)
{
    var response = await InvokeHostedAgentAsync(currentMessages, context.ModelName, toolDefs, agentSession, ct);
    totalTokens += response.Usage?.TotalTokens ?? 0;

    if (response.ToolCalls is { Count: > 0 })
    {
        // Add assistant message with tool calls to conversation
        currentMessages.Add(new ChatMessage { Role = Assistant, Content = response.Content ?? "", ToolCalls = response.ToolCalls });

        // Execute each tool
        foreach (var toolCall in response.ToolCalls)
        {
            var result = await _toolExecutor.ExecuteAsync(toolCall.Name, toolCall.Arguments, ct);
            allToolResults.Add(result);

            // Feed result back as a Tool message
            currentMessages.Add(new ChatMessage { Role = Tool, Content = result.Success ? result.Output : $"Error: {result.Error}", ToolCallId = toolCall.Id });
        }
        iterations++;
    }
    else
    {
        // No tool calls — this is the final response
        context.FinalResponse = response.Content;
        context.IsComplete = true;
        return context;
    }
}
```

Key points:
- The model decides when to call tools — our code just executes them
- Tool results go back into the conversation as `Role = Tool` messages
- The loop continues until the model stops requesting tools
- Token usage accumulates across all iterations

#### DefaultPromptComposer — Tool Injection

```csharp
public async Task<IReadOnlyList<ChatMessage>> ComposeAsync(PromptContext context, CancellationToken ct)
{
    var messages = new List<ChatMessage>();

    // 1. System prompt with skills
    var systemContent = DefaultSystemPrompt;
    var skills = await _skillLoader.GetActiveSkillsAsync(ct);
    if (skills.Count > 0)
        systemContent += $"\n\n# Active Skills\n{skillText}";

    // 2. Session summary
    if (!string.IsNullOrEmpty(context.SessionSummary))
        systemContent += $"\n\n# Previous Conversation Summary\n{context.SessionSummary}";

    messages.Add(new ChatMessage { Role = System, Content = systemContent });

    // 3. History + 4. Current message
    foreach (var msg in context.History) messages.Add(msg);
    messages.Add(new ChatMessage { Role = User, Content = context.UserMessage });

    return messages;
}
```

Point out: Tool definitions are NOT in the system prompt — they're passed as structured objects via the model API. The system prompt contains skills and context; tools are a separate channel.

#### Gateway DI — How All Tools Get Registered

In the Gateway's `Program.cs`, all tools are registered via the DI extensions:

```csharp
builder.Services.AddToolFramework();     // Registry + Executor + ApprovalPolicy
builder.Services.AddTool<FileSystemTool>();
builder.Services.AddTool<ShellTool>();
builder.Services.AddTool<WebTool>();
builder.Services.AddTool<SchedulerTool>();
builder.Services.AddAgentRuntime();      // Orchestrator + Runtime + PromptComposer
```

At startup, each `ITool` singleton is resolved and registered in the `ToolRegistry`. The executor can then find any tool by name.

### Live Demo

**Demo 1: Agent uses FileSystem tool**
1. Open the Blazor chat UI
2. Type: "List files in the current directory"
3. Watch the agent emit a `file_system` tool call → execute → show results
4. Point out the tool call/result in the response

**Demo 2: Agent uses Web tool**
1. Type: "What's on the front page of Hacker News?"
2. Watch the agent emit a `web_fetch` tool call → fetch → summarize
3. Point out: the agent decided to use the tool, fetched the page, then summarized

**Demo 3: Blocked command rejection**
1. Type: "Run `rm -rf /` on the server"
2. Watch the agent try to use the `shell` tool → ShellTool blocks it → agent reports the rejection
3. Point out: the security gate worked — the command never executed

### 🤖 Copilot Moment: Add Execution Duration Tracking

**When:** ~minute 40

**Context:** We've seen the agent loop execute tools. Now let's add observability.

**What to do:** Open `ToolExecutor.cs` and ask Copilot:

> In the ToolExecutor, add a method `GetExecutionStats()` that returns a dictionary of tool name → average execution duration. Track each tool's execution duration in a `ConcurrentDictionary<string, List<TimeSpan>>` field. Update it after each successful execution.

**Expected result:** Copilot adds a `_executionStats` field and a `GetExecutionStats()` method that calculates averages.

**Why it's interesting:** Shows how the chokepoint pattern (all tools through executor) makes it trivial to add cross-cutting concerns like metrics.

---

## Closing (8 min)

### Security Recap

| Threat | Tool | Defense |
|--------|------|---------|
| Path Traversal | FileSystemTool | `Path.GetFullPath` + workspace boundary check |
| Command Injection | ShellTool | Blocked command HashSet + timeout |
| SSRF | WebTool | Private IP blocklist + scheme validation |

Three threats. Three defenses. All implemented as input validation before execution.

### What We Built

- ✅ Tool abstraction layer (ITool, IToolExecutor, IToolRegistry)
- ✅ Approval policy gate (IToolApprovalPolicy)
- ✅ FileSystemTool with path traversal prevention
- ✅ ShellTool with command blocklist and timeout
- ✅ WebTool with SSRF protection
- ✅ SchedulerTool with job CRUD
- ✅ Agent reasoning loop (prompt → model → tool → loop)
- ✅ Prompt composition with tool injection

### Preview: Session 3

> "The agent has hands now. Next session: give it personality and memory."

Session 3 covers:
- **Skills** — YAML-based personality files that customize the agent's behavior
- **Memory** — Conversation summarization for long-term context
- **Skill loading** — Dynamic skill discovery and injection into the system prompt
