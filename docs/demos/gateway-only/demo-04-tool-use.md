# Demo 04 — Tool Use

**Level:** 🟡 Intermediate | **Time:** ~8 min  
**Shows:** Agent decides to call FileSystem and Shell tools, incorporates results into its response

---

## Prerequisites

- Gateway running on `http://localhost:5010`
- Ollama running with `llama3.2` pulled
- A model that supports tool calling (llama3.2 works; llama3.1 also works)

---

## What You'll See

Ask the agent a question it can't answer from knowledge alone — it will:
1. Decide which tool to call
2. Execute the tool (FileSystem or Shell)
3. Feed the result back to the model
4. Produce a final answer that incorporates real data from your machine

---

## Step 1 — Verify Tools Are Registered

```powershell
Invoke-RestMethod http://localhost:5010/api/tools | ConvertTo-Json -Depth 4
```

You'll see all registered tools with their schemas:

```json
[
  {
    "name": "file_system",
    "description": "Read, write, list, and manage files on the local file system.",
    "parameters": { ... }
  },
  {
    "name": "shell",
    "description": "Execute shell commands with safety restrictions.",
    "parameters": { ... }
  },
  {
    "name": "web",
    "description": "Make HTTP requests to external URLs.",
    "parameters": { ... }
  },
  ...
]
```

These are the `ToolDefinition` objects sent to the model on every request — the model reads them and decides when to call them.

---

## Step 2 — FileSystem Tool: List Files

Create a session and ask the agent to explore the current directory:

```powershell
$s = (Invoke-RestMethod http://localhost:5010/api/sessions -Method POST `
    -ContentType "application/json" -Body '{"title": "Tool demo"}').id

$r = Invoke-RestMethod http://localhost:5010/api/chat `
    -Method POST -ContentType "application/json" `
    -Body (@{
        sessionId = $s
        message   = "List the top-level files and folders in the current directory."
    } | ConvertTo-Json)

Write-Host "Tools used: $($r.toolCallCount)"
Write-Host "Response:"
$r.content
```

The agent will call `file_system` with action `list`, then describe what it found.

```
Tools used: 1
Response:
Here's what I found in the current directory:
- src/       (directory) — source code projects
- docs/      (directory) — documentation
- tests/     (directory) — test projects
- README.md  (file)
...
```

---

## Step 3 — FileSystem Tool: Read a File

```powershell
$r = Invoke-RestMethod http://localhost:5010/api/chat `
    -Method POST -ContentType "application/json" `
    -Body (@{
        sessionId = $s
        message   = "Read the README.md and give me a 2-sentence summary."
    } | ConvertTo-Json)

Write-Host "Tools used: $($r.toolCallCount)"
$r.content
```

The agent reads the actual file content, then summarizes it. `toolCallCount` will be 1.

---

## Step 4 — Multi-Tool Reasoning Loop

Ask a question that requires more than one tool call:

```powershell
$r = Invoke-RestMethod http://localhost:5010/api/chat `
    -Method POST -ContentType "application/json" `
    -Body (@{
        sessionId = $s
        message   = "How many .cs files are in the src/ directory? Count them."
    } | ConvertTo-Json)

Write-Host "Tools used: $($r.toolCallCount)"
$r.content
```

Typical flow:
1. Agent calls `file_system` → list `src/`
2. Agent calls `file_system` → list each subdirectory
3. Agent counts and reports

`toolCallCount` may be 3–5. You'll see multiple tool call rounds in the Gateway logs.

---

## Step 5 — Shell Tool (requires approval awareness)

The Shell tool has an approval policy. Depending on your configuration it may auto-approve in development or require explicit approval. Try:

```powershell
$r = Invoke-RestMethod http://localhost:5010/api/chat `
    -Method POST -ContentType "application/json" `
    -Body (@{
        sessionId = $s
        message   = "What version of .NET is installed? Check using the dotnet command."
    } | ConvertTo-Json)

$r.content
```

The agent will attempt `shell` with command `dotnet --version`. In development mode, shell commands are auto-approved. The response includes the real version output.

> **Safety note:** The `ShellTool` has a blocklist that prevents dangerous commands (`rm -rf`, `format`, `del /s`, etc.) and caps output at 10,000 characters.

---

## Step 6 — Inspect Tool Calls in History

```powershell
Invoke-RestMethod "http://localhost:5010/api/sessions/$s/messages" |
    Where-Object { $_.role -in "assistant","tool" } |
    ForEach-Object {
        Write-Host "[$($_.role.ToUpper())]"
        Write-Host ($_.content.Substring(0, [Math]::Min(120, $_.content.Length)))
        Write-Host "---"
    }
```

You'll see the interleaved `assistant` (tool-call decisions) and `tool` (tool results) messages that form the reasoning loop.

---

## What Just Happened

```
POST /api/chat { message: "List top-level files..." }
  └─> IAgentOrchestrator.ProcessAsync()
      ├─> ComposeAsync()           ← system + tools manifest + history + user
      ├─> IAgentProvider.CompleteAsync()
      │   └─> Ollama decides: call file_system { action: "list", path: "." }
      ├─> IToolExecutor.ExecuteAsync("file_system", args)
      │   └─> FileSystemTool.ExecuteAsync()  ← reads actual disk
      ├─> Add tool result to messages
      ├─> IAgentProvider.CompleteAsync() again ← model sees tool result
      │   └─> "Here's what I found: src/, docs/, ..."
      └─> return AgentResponse { toolCallCount: 1 }
```

The loop repeats (up to 10 iterations) until the model produces a final answer with no more tool calls.

---

## Next

→ **[Demo 05 — Skills & Personas](demo-05-skills.md)**: load a skill and change how the agent behaves.
