# Demo 03 — Tool Use & Tracing

**Level:** 🟡 Intermediate | **Time:** ~8 min  
**Shows:** Agent invoking FileSystem/Shell tools, multi-step reasoning loop, Aspire GenAI visualizer, distributed traces

---

## Prerequisites

- OpenClawNet running via AppHost
- Aspire Dashboard open
- Gateway URL set: `$gateway = "http://localhost:PORT"`
- Ollama model with native function calling: `gemma4:e2b` or `llama3.2`

> **Best model for this demo:** `gemma4:e2b` — Gemma 4's native function-calling support means tool decisions are more reliable and structured, with lower latency per tool call.

---

## What You'll See

Ask the agent questions that require it to look at real files. Watch the entire reasoning loop — tool decision, execution, result integration — visualized live in the Aspire Dashboard's **GenAI visualizer** and **Traces** view.

---

## Step 1 — Verify Tools Are Available

```powershell
Invoke-RestMethod "$gateway/api/tools" | Select-Object name, description | Format-Table -AutoSize
```

```
name            description
----            -----------
file_system     Read, write, list, and manage files on the local file system.
shell           Execute shell commands with safety restrictions.
web             Make HTTP requests to external URLs.
browser         Navigate and extract content from web pages using Playwright.
scheduler       Schedule and manage background jobs.
```

These 5 tool definitions are sent to the LLM in every request. The model decides when (and whether) to call them.

---

## Step 2 — Trigger a FileSystem Tool Call (Web UI)

Open the Web UI and start a new session. Send:

```
What .NET projects are in this solution? List them with their purposes.
```

Watch in real time:
1. The response starts streaming — then **pauses** while a tool runs
2. A tool indicator may appear: `🔧 Using: file_system`
3. The response resumes with actual file data

The agent called `file_system` with `action: list`, got the directory listing, and incorporated the real results into its answer.

---

## Step 3 — Multi-Step Reasoning (Web UI)

In the same session, send:

```
Read the README.md and summarize what OpenClawNet does in 3 bullet points.
```

Then:

```
Now look at the Gateway project's Program.cs and tell me which tools are registered.
```

For the second question, the agent will:
1. Call `file_system` to read `src/OpenClawNet.Gateway/Program.cs`
2. Parse the `AddTool<>` calls in the source
3. Report the actual registered tools from your real code

---

## Step 4 — Watch in the GenAI Visualizer

In the Aspire Dashboard → **Traces** tab, find the trace for one of those chat requests. Click it.

Look for spans labelled:
- `gen_ai.chat` — the LLM call (Ollama)
- `gen_ai.tool` — each tool execution

Click the `gen_ai.chat` span → select **GenAI** tab in the detail panel.

The **GenAI visualizer** (new in Aspire 13) shows:
- 📥 **Input messages**: system prompt + skill injections + conversation history
- 🔧 **Tool definitions**: all 5 tools the model was given with their JSON schemas
- 📤 **Model response**: the raw completion including tool call decisions
- ⚡ **Tool evaluations**: each tool call with arguments and result

This is how you see *exactly* what was sent to Ollama and what it responded with — including the tool-calling JSON the model generates internally.

---

## Step 5 — Multiple Tool Rounds via API

Trigger a multi-round tool loop directly through the API to get a clean trace:

```powershell
$s = (Invoke-RestMethod "$gateway/api/sessions" `
    -Method POST -ContentType "application/json" `
    -Body '{"title": "Tool trace demo"}').id

$r = Invoke-RestMethod "$gateway/api/chat" `
    -Method POST -ContentType "application/json" `
    -Body (@{
        sessionId = $s
        message   = "Count the number of .cs files in the src/ folder and tell me which project has the most."
    } | ConvertTo-Json)

Write-Host "Tool calls: $($r.toolCallCount)"
$r.content
```

`toolCallCount` will likely be 3–6 as the agent:
1. Lists `src/`
2. Lists each subdirectory
3. Counts files per project
4. Compares and reports

---

## Step 6 — Inspect the Reasoning Trace

After the request completes, go to Aspire Dashboard → Traces. Find the trace with the most spans.

You'll see a waterfall like:

```
POST /api/chat                                     [850ms]
  └─ AgentOrchestrator.ProcessAsync               [840ms]
      ├─ IConversationStore.GetMessagesAsync        [4ms]
      ├─ gen_ai.chat (round 1 — list src/)          [120ms]
      ├─ gen_ai.tool (file_system list src/)        [8ms]
      ├─ gen_ai.chat (round 2 — list subdirs)       [180ms]
      ├─ gen_ai.tool (file_system list gateway/)    [6ms]
      ├─ gen_ai.tool (file_system list agent/)      [5ms]
      ├─ gen_ai.chat (round 3 — count + compare)    [200ms]
      └─ IConversationStore.AddMessageAsync          [3ms]
```

Each `gen_ai.chat` span is one model call. Each `gen_ai.tool` span is one tool execution. This is the **reasoning loop** made visible.

---

## Step 7 — Shell Tool

The Shell tool executes real commands. Try:

```powershell
$r = Invoke-RestMethod "$gateway/api/chat" `
    -Method POST -ContentType "application/json" `
    -Body (@{
        sessionId = $s
        message   = "What .NET SDK version is installed? Use the dotnet command."
    } | ConvertTo-Json)

$r.content
```

The agent runs `dotnet --version` via the `ShellTool` and reports the actual installed version.

> **Safety:** ShellTool blocks dangerous commands (`rm -rf`, `del /s`, `format`, `shutdown`, etc.) and caps output at 10,000 characters with a 30-second timeout.

---

## What Just Happened

```
LLM receives: [system] + [tool definitions for all 5 tools] + [user: "count .cs files"]
  └─> Model responds: { "tool_calls": [{ "name": "file_system", "args": {"action":"list","path":"src/"} }] }
  └─> IToolExecutor.ExecuteAsync("file_system", args)
      └─> FileSystemTool reads disk
      └─> Returns: "src/OpenClawNet.Agent/ src/OpenClawNet.Gateway/ ..."
  └─> Append tool result to messages
  └─> LLM call #2: [... + tool result]
      └─> Model: calls file_system again for each subdir
  └─> ... (loop up to 10 iterations)
  └─> LLM: "OpenClawNet.Agent has the most with 11 .cs files."
```

The Aspire GenAI visualizer shows every step of this loop with full payloads.

---

## Next

→ **[Demo 04 — Skills & Personas](demo-04-skills.md)**: change how the agent behaves by enabling a skill at runtime.
