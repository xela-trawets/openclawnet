# Demo 02 — First Chat

**Level:** 🟢 Beginner | **Time:** ~5 min  
**Shows:** Blazor Web UI, real-time token streaming, session creation, multi-turn conversation

---

## Prerequisites

- OpenClawNet running: `aspire start src\OpenClawNet.AppHost`
- Ollama running: `ollama serve` + model pulled: `ollama pull gemma4:e2b` (or `llama3.2`)
- Web UI URL copied from the Aspire Dashboard

---

## Part A — Chat in the Web UI

### 1. Open the Web UI

Navigate to the `web` URL from the Aspire Dashboard.

### 2. Start a New Session

Click **New Chat** (or the `+` button in the sidebar).

A new conversation session is created in SQLite — you'll see it appear in the sidebar.

### 3. Send Your First Message

Type in the chat input and press Enter:

```
What is .NET Aspire and why does it exist?
```

Watch the response stream in **token by token** — you'll see individual words appear as the model generates them. This is HTTP SSE streaming (`POST /api/chat/stream`) delivering NDJSON events in real time.

### 4. Multi-Turn Conversation

The session remembers context. Send a follow-up:

```
What are its main components?
```

Then:

```
How does it help with local development?
```

Each message loads the full conversation history from SQLite, assembles a prompt with `IPromptComposer`, and sends it to Ollama.

### 5. Rename the Session

In the sidebar, click the session name and rename it to something descriptive like **"Aspire deep-dive"**.

---

## Part B — Chat via the REST API

While the Web UI works with the same Gateway, you can also call the REST API directly. Get the gateway URL from the Aspire Dashboard:

```powershell
$gateway = "http://localhost:GATEWAY-PORT"
```

### Create a session and chat:

```powershell
$s = (Invoke-RestMethod "$gateway/api/sessions" `
    -Method POST -ContentType "application/json" `
    -Body '{"title": "API demo"}').id

$r = Invoke-RestMethod "$gateway/api/chat" `
    -Method POST -ContentType "application/json" `
    -Body (@{
        sessionId = $s
        message   = "Explain IAsyncEnumerable in one sentence."
    } | ConvertTo-Json)

$r.content
```

```json
{
  "content": "IAsyncEnumerable<T> is a C# interface for async sequences...",
  "toolCallCount": 0,
  "totalTokens": 62
}
```

### List all sessions (Web UI + API sessions combined):

```powershell
Invoke-RestMethod "$gateway/api/sessions" | 
    Select-Object id, title, createdAt | 
    Format-Table -AutoSize
```

Sessions created via the Web UI appear here — they all share the same `IConversationStore`.

---

## Part C — Watch It in the Dashboard

After sending a few messages, switch to the Aspire Dashboard → **Traces** tab.

You'll see a trace for each `POST /api/chat` request showing:
- `POST /api/chat` — top-level span
- Agent orchestration span
- `IAgentProvider.CompleteAsync` — the Ollama call with duration
- SQLite query spans (read history + write messages)

Click any trace to expand the waterfall view — you can see exactly how long the model took to respond vs. how long history retrieval took.

---

## What Just Happened

```
[Web UI] User types message → HTTP POST to /api/chat/stream (NDJSON)
  └─> IAgentOrchestrator.StreamAsync()
      ├─> IConversationStore.AddMessageAsync()    ← save user message
      ├─> IConversationStore.GetMessagesAsync()   ← load history
      ├─> IPromptComposer.ComposeAsync()          ← system + skills + history
      ├─> IAgentProvider.StreamAsync()             ← Ollama SSE stream
      │   └─> yields tokens → HTTP stream → browser as NDJSON
      ├─> IConversationStore.AddMessageAsync()    ← save assistant message
      └─> yields Complete event → UI shows full response
```

---

## Next

→ **[Demo 03 — Tool Use & Tracing](demo-03-tool-tracing.md)**: ask the agent to read project files and watch the tool calls appear in the Aspire GenAI visualizer.
