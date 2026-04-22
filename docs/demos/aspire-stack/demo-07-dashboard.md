# Demo 07 — Aspire Dashboard Deep-Dive

**Level:** 🔴 Advanced | **Time:** ~10 min  
**Shows:** GenAI visualizer, structured log correlation, distributed trace waterfall, metrics — all new in Aspire 13

---

## Prerequisites

- OpenClawNet running via AppHost
- Aspire Dashboard open at `http://localhost:15888`
- At least one tool-calling request fired (run Demo 03 first if you haven't)

---

## What's New in Aspire 13.2.2

Aspire 13 introduced first-class AI observability built on OpenTelemetry. These features are automatically available in OpenClawNet with zero configuration because the AppHost instruments the `gateway` and `web` services:

- **GenAI visualizer** — shows full LLM input, output, tool definitions, and tool evaluations per `gen_ai.chat` span
- **Linked log entries** — logs emitted during a span are clickable from the trace view
- **Structured log filtering** — filter by `{HostName}`, `{Service}`, span ID, or any structured field
- **Token metrics** — `gen_ai.client.token.usage` histogram, visible in the Metrics tab

---

## Step 1 — Fire a Tool-Calling Request

Generate rich trace data:

```powershell
$gateway = "http://localhost:PORT"  # get from Aspire Dashboard
$s = (Invoke-RestMethod "$gateway/api/sessions" -Method POST `
    -ContentType "application/json" `
    -Body '{"title": "Dashboard demo session"}').id

Invoke-RestMethod "$gateway/api/chat" -Method POST -ContentType "application/json" `
    -Body (@{
        sessionId = $s
        message   = "List the files in src/OpenClawNet.Gateway then summarise what the Gateway project does"
    } | ConvertTo-Json)
```

This should trigger 2–4 tool calls: `list_directory`, `read_file` × N.

---

## Step 2 — Trace Waterfall

1. Open Aspire Dashboard → **Traces**
2. Filter by resource: `gateway`
3. Click the trace for your request (look for `POST /api/chat`)

You'll see a waterfall like:

```
POST /api/chat                                    550ms
  agent.run                                       530ms
    gen_ai.chat  [1 tool call]                    210ms   ← LLM decides to call list_directory
    tool.execute list_directory                    15ms
    gen_ai.chat  [1 tool call]                    180ms   ← LLM decides to call read_file
    tool.execute read_file                          8ms
    gen_ai.chat  [final response]                 120ms
```

Each `gen_ai.chat` span captures a **complete LLM turn** — useful for understanding where latency comes from.

---

## Step 3 — GenAI Visualizer

Click any `gen_ai.chat` span. The right panel shows a **GenAI Visualizer** section with three tabs:

### Input tab
The exact messages array sent to Ollama:
```json
[
  { "role": "system", "content": "You are a helpful assistant..." },
  { "role": "user",   "content": "List the files in src/OpenClawNet.Gateway..." }
]
```

### Tools tab  
All tools registered for this call, in JSON Schema format:
```json
[
  {
    "type": "function",
    "function": {
      "name": "list_directory",
      "description": "Lists files in a directory",
      "parameters": { "type": "object", "properties": { "path": { "type": "string" } } }
    }
  },
  ...
]
```

> With `gemma4:e2b`, these are sent natively to Ollama's `/api/chat` endpoint. The model has been fine-tuned to understand this schema and respond with structured tool calls.

### Output tab
The model's response, including tool call decisions:
```json
{
  "role": "assistant",
  "tool_calls": [
    { "function": { "name": "list_directory", "arguments": { "path": "src/OpenClawNet.Gateway" } } }
  ]
}
```

---

## Step 4 — Linked Log Entries

Still in the trace view, scroll down past the GenAI Visualizer. You'll see a **Logs** section — these are structured log entries that were emitted while this span was active.

Click any log entry to jump to **Logs** tab with the span pre-filtered. This is how you correlate a specific LLM call with the surrounding application logs.

---

## Step 5 — Structured Log Filtering

Open Aspire Dashboard → **Structured Logs** tab.

Filter options:

| Filter | Example | Description |
|---|---|---|
| Resource | `gateway` | Logs from the gateway service only |
| Level | `Warning` | Show only warnings and above |
| Span ID | `a3f8...` | All logs within a single trace span |
| Message | `tool.execute` | Search log message text |
| Attribute | `session.id=...` | Filter by any structured field |

Example: type `tool` in the message filter to see every tool execution log with its arguments and result duration.

---

## Step 6 — Token Usage Metrics

Open Aspire Dashboard → **Metrics** tab. Select resource `gateway`.

Find the `gen_ai.client.token.usage` histogram. This shows:

- Prompt tokens per request (how much context was sent)
- Completion tokens per request (how long the response was)
- Broken down by model

Compare token counts across different models to understand cost/quality tradeoffs. `gemma4:e2b` typically uses fewer tokens per tool-calling turn than larger models due to its native function-calling format.

---

## Step 7 — Cross-Service Trace (Gateway + Web)

Send a request through the Web UI rather than directly to the Gateway. The trace will now span **two services**:

1. Open the Web UI (URL from Aspire Dashboard)
2. Type a message in the chat
3. In Aspire Dashboard → Traces, find a trace with both `web` and `gateway` in the service column

You'll see the HTTP call from `web → gateway` as a parent span, with all the `gen_ai.chat` child spans inside `gateway`.

---

## Step 8 — Aspire Dashboard MCP (Bonus)

If you've run `aspire agent init` (see Demo 01 Step 5), the Aspire Dashboard is also exposed as an MCP server. You can query live resource state from GitHub Copilot CLI:

```
/ask What spans are currently in the OpenClawNet gateway traces?
```

The MCP server reads your running Aspire session and returns live data.

---

## Summary — What Aspire 13 Gives You for Free

| Feature | Dashboard location | What it shows |
|---|---|---|
| GenAI Visualizer | Traces → span detail | Full LLM prompt, tools, response |
| Token metrics | Metrics → `gen_ai.client.token.usage` | Prompt + completion tokens per model |
| Linked logs | Traces → span detail → Logs section | App logs emitted during each LLM call |
| Structured log filter | Structured Logs tab | Filter by any attribute, span, level |
| Cross-service traces | Traces (multi-resource) | Web → Gateway → Tool spans in one waterfall |

All of this is zero-config for OpenClawNet — the AppHost automatically wires up OpenTelemetry for all child services.

---

## All Demos Complete 🎉

| # | Demo | Concepts |
|---|---|---|
| 01 | [Launch & Setup](demo-01-launch.md) | AppHost, Dashboard, MCP agent |
| 02 | [First Chat](demo-02-first-chat.md) | Web UI, REST API, streaming |
| 03 | [Tool Tracing](demo-03-tool-tracing.md) | Tool loop, GenAI visualizer |
| 04 | [Skills](demo-04-skills.md) | Enable/disable, hot reload |
| 05 | [Webhooks](demo-05-webhooks.md) | Event-driven agent |
| 06 | [Provider Switch](demo-06-provider-switch.md) | Swap LLM at config level |
| 07 | [Dashboard Deep-Dive](demo-07-dashboard.md) | Observability, metrics, traces |
