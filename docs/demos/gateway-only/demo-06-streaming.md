# Demo 06 — Real-Time Streaming

**Level:** 🟡 Intermediate | **Time:** ~5 min  
**Shows:** `POST /api/chat/stream` with HTTP SSE (NDJSON), token-by-token streaming, tool call events

---

## Prerequisites

- Gateway running on `http://localhost:5010`
- Ollama running with `llama3.2` pulled
- curl or PowerShell for HTTP requests

---

## What You'll See

Instead of waiting for a complete response, tokens stream back one by one through HTTP Server-Sent Events (SSE) as NDJSON. You'll also see tool events emitted in real time as the agent makes tool calls.

---

## Option A — PowerShell with Invoke-WebRequest

The easiest option. Create a session and stream the response:

```powershell
$gateway = "http://localhost:5010"

# Create session
$sess = (Invoke-RestMethod "$gateway/api/sessions" `
    -Method POST -ContentType "application/json" `
    -Body '{"title": "Streaming demo"}').id

Write-Host "Session: $sess`n"

# Stream chat response (NDJSON)
$response = Invoke-WebRequest "$gateway/api/chat/stream" `
    -Method POST `
    -ContentType "application/json" `
    -Headers @{ "Accept" = "application/x-ndjson" } `
    -Body (@{
        sessionId = $sess
        message   = "Explain .NET Aspire in 3 bullet points."
    } | ConvertTo-Json)

# Parse NDJSON lines
$response.Content -split "`n" | Where-Object { $_ } | ForEach-Object {
    $evt = $_ | ConvertFrom-Json
    switch ($evt.type) {
        "content"        { Write-Host -NoNewline $evt.content }
        "tool_start"     { Write-Host "`n[tool: $($evt.toolName)] " }
        "tool_complete"  { Write-Host "[done] " }
        "complete"       { Write-Host "`n`n[stream complete]`n" }
    }
}
```

Output (live tokens):
```
Aspire is a cloud-ready framework for:
• Building distributed .NET applications
• Observable and resilient patterns
• Integrated service discovery and health checks
```

---

## Option B — curl with Streaming

For real-time token display:

```bash
curl -X POST "http://localhost:5010/api/chat/stream" \
  -H "Content-Type: application/json" \
  -H "Accept: application/x-ndjson" \
  -d '{
    "sessionId": "550e8400-e29b-41d4-a716-446655440000",
    "message": "Write a haiku about distributed systems."
  }' | jq -Rs 'split("\n") | .[] | select(length > 0) | fromjson | 
    if .type == "content" then .content else "\n[" + .type + "]\n" end' -r
```

Each line is NDJSON — curl reads them as they arrive.

---

## Option C — Node.js Script (fetch + streaming)

If you prefer a more traditional streaming pattern:

```javascript
const gateway = "http://localhost:5010";

// Create session
const sessResp = await fetch(`${gateway}/api/sessions`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title: "Node streaming demo" })
});
const { id: sessId } = await sessResp.json();

console.log(`Session: ${sessId}\n`);

// Stream
const streamResp = await fetch(`${gateway}/api/chat/stream`, {
    method: "POST",
    headers: {
        "Content-Type": "application/json",
        "Accept": "application/x-ndjson"
    },
    body: JSON.stringify({
        sessionId: sessId,
        message: "What are the SOLID principles? Be brief."
    })
});

const reader = streamResp.body.getReader();
const decoder = new TextDecoder();
let buffer = "";

while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    
    buffer += decoder.decode(value, { stream: true });
    const lines = buffer.split("\n");
    
    // Keep the last incomplete line in buffer
    buffer = lines.pop() || "";
    
    for (const line of lines) {
        if (!line) continue;
        const evt = JSON.parse(line);
        if (evt.type === "content")       process.stdout.write(evt.content);
        if (evt.type === "tool_start")    process.stdout.write(`\n[tool: ${evt.toolName}] `);
        if (evt.type === "tool_complete") process.stdout.write("[done] ");
        if (evt.type === "complete")      console.log("\n\n[stream complete]");
    }
}
```

Run it:
```bash
node stream-demo.js
```

---

## NDJSON Event Format

The stream sends newline-delimited JSON. Each event is one line:

| `type` | When | Payload |
|--------|------|---------|
| `content` | Each token generated | `{ "type": "content", "content": "The token text" }` |
| `tool_start` | Agent begins a tool call | `{ "type": "tool_start", "toolName": "file_system" }` |
| `tool_complete` | Tool finishes | `{ "type": "tool_complete", "toolName": "file_system" }` |
| `complete` | Final response ready | `{ "type": "complete", "content": "Full response text" }` |
| `error` | Any error | `{ "type": "error", "content": "Error message" }` |

---

## What Just Happened

```
POST /api/chat/stream { sessionId, message }
  └─> ChatStreamEndpoints → IAgentOrchestrator.StreamAsync()
      └─> DefaultAgentRuntime.ExecuteStreamAsync()
          ├─> IAgentProvider.StreamAsync()  ← Ollama SSE stream
          │   yield ContentDelta per token → NDJSON line
          ├─> IToolExecutor.ExecuteAsync()
          │   yield ToolCallStart, ToolCallComplete → NDJSON lines
          └─> yield Complete → NDJSON line
              ← HTTP response body (streaming)
```

The HTTP response is `200 OK` with `Content-Type: application/x-ndjson` and the body streams live events as they happen — no buffering, no connection overhead compared to WebSocket.

The Blazor web UI (Aspire Stack → Demo 02) uses this exact endpoint — every chat message you type streams through `POST /api/chat/stream`.

---

## Next

→ **[Demo 07 — Provider Switch](demo-07-provider-switch.md)**: change the LLM provider with a config change only.
