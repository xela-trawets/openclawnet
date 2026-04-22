# Demo 02 — First Chat

**Level:** 🟢 Beginner | **Time:** ~3 min  
**Shows:** `POST /api/chat`, single-turn LLM call through the full agent stack

---

## Prerequisites

- Gateway running on `http://localhost:5010`
- Ollama running: `ollama serve` + model pulled: `ollama pull llama3.2`

---

## What You'll See

Send one message. The agent composes a prompt, calls Ollama, and returns the response. One round trip through `IAgentOrchestrator → IAgentProvider → Ollama`.

---

## Steps

### 1. Create a Session

Every conversation belongs to a session. Create one first:

```powershell
$session = Invoke-RestMethod http://localhost:5010/api/sessions `
    -Method POST `
    -ContentType "application/json" `
    -Body '{"title": "My First Chat"}'

$sessionId = $session.id
Write-Host "Session ID: $sessionId"
```

Expected:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "title": "My First Chat",
  "createdAt": "2026-04-13T18:00:00Z"
}
```

---

### 2. Send a Message

```powershell
$response = Invoke-RestMethod http://localhost:5010/api/chat `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        sessionId = $sessionId
        message   = "What is .NET Aspire in one sentence?"
    } | ConvertTo-Json)

$response.content
```

Expected (Ollama will vary):
```
.NET Aspire is a cloud-ready stack for building distributed, observable 
.NET applications with built-in service discovery and health checks.
```

---

### 3. Inspect the Full Response

```powershell
$response | ConvertTo-Json
```

```json
{
  "content": ".NET Aspire is...",
  "toolCallCount": 0,
  "totalTokens": 87
}
```

- `toolCallCount: 0` — the agent answered directly, no tools needed
- `totalTokens` — total input + output tokens consumed

---

### 4. Ask a Follow-Up (Same Session)

```powershell
$r2 = Invoke-RestMethod http://localhost:5010/api/chat `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        sessionId = $sessionId
        message   = "What are its main components?"
    } | ConvertTo-Json)

$r2.content
```

The agent remembers "Aspire" from the previous turn — history is loaded from SQLite automatically.

---

## What Just Happened

```
POST /api/chat { sessionId, message }
  └─> ChatEndpoints → IAgentOrchestrator.ProcessAsync()
      ├─> IConversationStore.AddMessageAsync()     ← save user message to SQLite
      ├─> IConversationStore.GetMessagesAsync()    ← load full history
      ├─> IPromptComposer.ComposeAsync()           ← system + history + user
      ├─> IAgentProvider.CompleteAsync()            ← Ollama: llama3.2
      │   └─> HTTP POST http://localhost:11434/api/chat
      ├─> IConversationStore.AddMessageAsync()     ← save assistant message
      └─> return AgentResponse { content, toolCallCount, totalTokens }
```

---

## Override the Model

You can override the model (or provider) per-request:

```powershell
$body = @{
    sessionId = $sessionId
    message   = "Hello"
    model     = "llama3.1"          # any model pulled in Ollama
    provider  = "ollama"            # ollama | foundry-local | azure-openai
} | ConvertTo-Json
```

---

## Next

→ **[Demo 03 — Multi-Turn Conversation](demo-03-multi-turn.md)**: inspect history, manage sessions.
