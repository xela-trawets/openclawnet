# 🎤 Session 2: Speaker Script — Tools + Agent Workflows

**Duration:** 50 minutes | **Presenters:** Bruno (lead) + Co-presenter (demos + audience)

---

## Pre-Session Checklist (10 min before)

- [ ] `aspire run` — verify Session 1 app is running
- [ ] Open browser to `https://localhost:{port}/api/tools` — confirm tool manifest loads
- [ ] Open `ShellTool.cs` in VS Code for Copilot Moment #1
- [ ] Open `ToolExecutor.cs` in VS Code for Copilot Moment #2
- [ ] Blazor chat UI open and functional
- [ ] Prepare HTTP client (browser or curl) for API demos
- [ ] Have Session 1 recap slide ready

---

## Timeline

### 0:00–2:00 — Opening (2 min)

**Bruno:**
> "Welcome back! In Session 1, we built a chatbot — it talks, it streams, it stores history. But it's limited to generating text. Today we cross the most important line in AI agents: we give it the ability to **act**."

- Show Session 1 recap slide (Models, Storage, Gateway, UI — all ✅)
- Transition: *"The difference between a chatbot and an agent? One word: **tools**."*

**Co-presenter:**
- Confirm audience can see the slides
- Quick poll: "Who has built a tool-calling agent before?" (gauge experience)

---

### 2:00–14:00 — Stage 1: Tool Architecture (12 min)

**Bruno (concepts + code, 12 min):**

**[0:02–0:05] Chatbot vs Agent concept (3 min)**
- Explain: "A chatbot generates text. An agent decides it needs to *do something* and requests a tool call. The model doesn't execute anything — it emits a structured request that our code executes."
- Show the Chatbot vs Agent comparison table on slides

**[0:05–0:08] ITool interface deep dive (3 min)**
- Walk through `ITool.cs` — four members: Name, Description, Metadata, ExecuteAsync
- Point out: `ExecuteAsync` returns `ToolResult`, not a raw string
- Show `IToolApprovalPolicy` — the security gate interface
- Mention `AlwaysApprovePolicy` default, ShellTool opts into approval

**[0:08–0:11] Registry vs Executor separation (3 min)**
- Walk through `IToolRegistry.cs` — Register, GetTool, GetAllTools, GetToolManifest
- Walk through `IToolExecutor.cs` — the chokepoint pattern
- Show `ToolExecutor.cs` code: lookup → approval → execute → log
- Key insight: *"Registry is about what exists; executor is about how to run safely"*

**[0:11–0:14] Tools.Core DI wiring (3 min)**
- Show `ToolsServiceCollectionExtensions.cs`
- `AddToolFramework()` — Registry (singleton), Executor (scoped), ApprovalPolicy (singleton)
- `AddTool<T>()` — registers individual tools as singletons

### [DEMO] Tool Manifest Endpoint (at ~minute 12)

**Co-presenter:**
1. Open browser to `GET /api/tools`
2. Show the JSON response — list of tool metadata
3. Point out: "This is what the model sees when deciding which tool to call"
4. Highlight name, description, parameter schema for each tool

**[FALLBACK]** If API not responding:
- Show a pre-captured screenshot of the JSON response
- Explain: "The manifest includes name, description, and JSON Schema for every registered tool"

---

### 14:00–29:00 — Stage 2: Built-in Tools + Security (15 min)

**Bruno (code walkthroughs, 15 min):**

**[0:14–0:15] Three Security Threats intro (1 min)**
- Show the threats table: Path Traversal, Command Injection, SSRF
- *"Every tool validates inputs before execution. Fail fast, fail safe."*

**[0:15–0:18] FileSystemTool walkthrough (3 min)**
- Show `BlockedPaths` array (.env, .git, appsettings.Production)
- Walk through `ResolvePath` method — `Path.GetFullPath` + workspace boundary check
- Explain: "GetFullPath resolves `..` segments. Then we check it stays inside the workspace."
- Mention: 1MB file size limit, three operations (read, write, list)

**[0:18–0:21] ShellTool walkthrough (3 min)**
- Show the `BlockedCommands` HashSet — 14 dangerous commands
- Walk through `IsSafeCommand` — extract first word, strip path, check blocklist
- Point out: 30s timeout, 10K char output limit, `RequiresApproval = true`
- Cross-platform: `cmd.exe /c` on Windows, `/bin/sh -c` on Linux

**[0:21–0:23] WebTool walkthrough (2 min)**
- Show `IsLocalUri` method — blocks localhost, 127.0.0.1, private ranges
- Scheme validation: only http/https
- 50K char response limit, 15s timeout

**[0:23–0:24] SchedulerTool overview (1 min)**
- Three actions: create, list, cancel
- EF Core persistence, cron expressions, ISO 8601 one-time jobs

### 🤖 Copilot Moment #1 (~minute 22)

**Co-presenter leads, Bruno narrates:**

> "We just walked through the ShellTool blocklist. Let's extend it live with Copilot."

1. Open `ShellTool.cs`, cursor inside `BlockedCommands` HashSet
2. Open Copilot Chat, type the prompt (see [copilot-prompts.md](./copilot-prompts.md))
3. Accept the suggestion — should add `wget`, `curl` with a comment
4. *"Small change, big security impact. Extending the defense is trivial with good architecture."*

**[FALLBACK]** If Copilot doesn't generate expected output:
- Manually add `"wget", "curl"` to the HashSet
- Add comment: `// Network tools blocked to prevent data exfiltration`
- Explain: "The pattern is the same — add to the set, done"

### [DEMO] Security Attack Demos (~minute 25)

**Co-presenter demonstrates, Bruno narrates:**

1. **Path traversal attempt:**
   - Chat: "Read the file `../../etc/passwd`"
   - Show: FileSystemTool blocks it — "Path outside workspace"

2. **Command injection attempt:**
   - Chat: "Run `rm -rf /` on the server"
   - Show: ShellTool blocks it — command in blocklist

3. **SSRF attempt:**
   - Chat: "Fetch `http://127.0.0.1:8080/admin`"
   - Show: WebTool blocks it — local address rejected

**[FALLBACK]** If Blazor UI isn't working:
- Use curl against the API directly
- Show pre-recorded terminal output of each blocked attempt

---

### 29:00–44:00 — Stage 3: Agent Loop + Integration (15 min)

**Bruno (architecture + code, 15 min):**

**[0:29–0:32] Agent reasoning loop concept (3 min)**
- Show the flowchart diagram on slides
- Walk through: compose prompt → call model → tool calls? → execute → loop back
- *"This loop repeats until the model has no more tool calls, or we hit the safety limit."*
- `MaxToolIterations = 10` — without this, a confused model loops forever

**[0:32–0:35] AgentOrchestrator code (3 min)**
- Show `ProcessAsync` method — creates context, delegates to runtime
- *"The orchestrator doesn't know about tools, models, or prompts. It's a coordinator."*

**[0:35–0:38] DefaultAgentRuntime — the core loop (3 min)**
- Walk through the `while` loop in detail
- Key points:
  - Model decides when to call tools — our code just executes them
  - Tool results go back as `Role = Tool` messages
  - Token usage accumulates across iterations

**[0:38–0:40] DefaultPromptComposer (2 min)**
- Show how system prompt is built: base + skills + summary
- *"Tool definitions are NOT in the system prompt — they're structured objects passed via the model API."*

**[0:40–0:41] Gateway DI registration (1 min)**
- Show `Program.cs`: `AddToolFramework()`, `AddTool<>()` calls, `AddAgentRuntime()`

### [DEMO] Agent Uses Tools (~minute 35)

**Co-presenter demonstrates:**

1. **FileSystem tool:** Chat: "List files in the current directory"
   - Watch the `file_system` tool call → execute → show results
2. **Web tool:** Chat: "What's on the front page of Hacker News?"
   - Watch the `web_fetch` tool call → fetch → summarize
3. Point out: the agent decided which tool to use, executed it, then responded

**[FALLBACK]** If tool calls aren't working:
- Show pre-recorded session with tool call/result visible
- Explain the flow using the architecture diagram

### 🤖 Copilot Moment #2 (~minute 40)

**Co-presenter leads, Bruno narrates:**

> "We've seen the agent loop execute tools. Now let's add observability."

1. Open `ToolExecutor.cs`
2. Open Copilot Chat, type the prompt (see [copilot-prompts.md](./copilot-prompts.md))
3. Accept the suggestion — should add `ConcurrentDictionary` + `GetExecutionStats()`
4. *"Because all tools go through the executor, we add metrics once and get stats for everything. That's the chokepoint pattern payoff."*

**[FALLBACK]** If Copilot doesn't generate expected output:
- Explain the concept verbally using the existing `Stopwatch` code
- *"The infrastructure is already here — the stopwatch in every execution. We'd just aggregate it."*

---

### 44:00–50:00 — Closing (6 min)

**Bruno:**

**[0:44–0:46] Security recap (2 min)**
- Show the security recap table: 3 threats, 3 defenses
- *"Three threats. Three defenses. All implemented as input validation before execution."*

**[0:46–0:48] What we built checklist (2 min)**
- Walk through the 8-item checklist:
  - ✅ Tool abstraction layer
  - ✅ Approval policy gate
  - ✅ FileSystemTool, ShellTool, WebTool, SchedulerTool
  - ✅ Agent reasoning loop
  - ✅ Prompt composition with tool injection

**[0:48–0:50] Session 3 preview (2 min)**
> "The agent has hands now. Next session: give it personality and memory."

- **Skills** — YAML personality files that customize behavior
- **Memory** — Conversation summarization for long-term context

**Co-presenter:**
- Share repo link: `github.com/elbruno/openclawnet`
- Remind audience to check out `session-2-complete` tag
- Thank the audience, invite questions

---

## Key Talking Points (Quick Reference)

- **Chatbot vs Agent:** Text generation vs tool use — the model requests, our code executes
- **Chokepoint pattern:** All tools through the executor → add logging/metrics/approval once
- **Defense in depth:** Blocklists + path validation + timeouts + output limits
- **The loop:** Prompt → model → tool calls? → execute → loop. Max 10 iterations.
- **Separation of concerns:** Registry (what exists) vs Executor (how to run safely)
