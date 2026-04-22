# Demo 09 — Full Aspire Stack

**Level:** 🔴 Advanced | **Time:** ~10 min  
**Shows:** .NET Aspire AppHost, all services orchestrated together, Blazor Web UI with streaming chat

---

## Prerequisites

- .NET 10 SDK with Aspire workload: `dotnet workload install aspire`
- Ollama running: `ollama serve` + `ollama pull llama3.2`
- Docker Desktop (optional — Aspire can run without containers for this stack)
- All other demos completed (gives you confidence each layer works)

---

## What You'll See

One command starts everything: Gateway + Web UI + Aspire Dashboard. The Blazor UI gives you a full chat experience with real-time token streaming, session history, and a visual dashboard showing service health.

---

## Step 1 — Install the Aspire Workload

```powershell
dotnet workload install aspire
dotnet workload list  # confirm: aspire listed
```

---

## Step 2 — Start the Full Stack

```powershell
aspire start src\OpenClawNet.AppHost
```

Expected output:
```
Building...
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 9.x.x
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application starting.
info: Aspire.Hosting.DistributedApplication[0]
      Resources:
        gateway: http://localhost:XXXXX (started)
        web:     http://localhost:XXXXX (started)
info: Aspire.Hosting.DistributedApplication[0]
      Login to the dashboard at:
      http://localhost:15888/login?t=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

---

## Step 3 — Open the Aspire Dashboard

Go to the dashboard URL printed in the output. You'll see:

- **Resources** tab: Gateway + Web services with health status ✅
- **Logs** tab: Live logs from both services
- **Traces** tab: Distributed traces of requests (OpenTelemetry)
- **Metrics** tab: Request counts, response times

Click on any resource to see its live logs streaming.

---

## Step 4 — Open the Blazor Chat UI

Find the `web` service URL in the dashboard (or in the console output) and open it in your browser.

The UI shows:
- **Toolbar**: provider/model selection, active jobs, connection status
- **Sidebar**: conversation sessions list
- **Main area**: chat messages with streaming
- **New Chat** button: creates a new session

---

## Step 5 — Chat with Streaming

Type a message and hit Enter (or click Send). Watch:
1. Your message appears immediately
2. The assistant starts streaming tokens — each word/chunk appears as it's generated via HTTP SSE
3. If the agent uses a tool, you'll see a `🔧 Using tool: file_system` indicator appear

Try:
- *"What .NET projects are in this repo?"* — triggers FileSystem tool, streams results
- *"Explain the IAgentProvider interface"* — direct response, pure streaming
- *"Read the README and give me the TL;DR"* — FileSystem + streaming summary

---

## Step 6 — Use Both the UI and API Simultaneously

The Gateway is the backend for both the UI and direct API calls. While the chat UI is open, also call the REST API:

```powershell
# Find your gateway port from the dashboard or AppHost output
$gw = "http://localhost:GATEWAY-PORT"

# List sessions created via the UI
Invoke-RestMethod "$gw/api/sessions" | Select-Object id, title, createdAt

# Call the API directly while the UI is also connected
$s = (Invoke-RestMethod "$gw/api/sessions" -Method POST `
    -ContentType "application/json" -Body '{"title": "API session"}').id
(Invoke-RestMethod "$gw/api/chat" -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s; message="Hello from the API!" } | ConvertTo-Json)).content
```

The UI and API share the same SQLite database and agent runtime.

---

## Step 7 — Explore Distributed Traces

In the Aspire Dashboard → **Traces** tab:

1. Send a message from the UI
2. Refresh Traces
3. Click on a trace to see the full request tree:
   - `POST /api/chat` → Gateway
   - Agent runtime span
   - Model call span (Ollama)
   - Tool execution spans (if any)
   - SQLite query spans

This is OpenTelemetry out of the box — Aspire's `AddServiceDefaults()` sets it up automatically.

---

## Step 8 — Watch Service Health

The AppHost monitors health via:
- `GET /health` on Gateway (every 5s)
- `GET /health` on Web (every 5s)

Stop Ollama (`Ctrl+C` in the Ollama terminal) and watch the Gateway health check turn yellow/red in the dashboard within 10 seconds.

Restart Ollama — it recovers automatically.

---

## Full Architecture Running

```
┌─────────────────────────────────────┐
│  Browser                            │
│  └─> Blazor UI (:XXXX)              │
│      ├─ Toolbar (provider, model)   │
│      └─> HTTP POST /api/chat/stream │
│          └─> Server-Sent Events     │
└──────────────┬──────────────────────┘
               │ HTTP + NDJSON streaming
┌──────────────▼──────────────────────┐
│  OpenClawNet.Gateway (:YYYY)        │
│  ├─> IAgentOrchestrator            │
│  ├─> IToolRegistry (5 tools)       │
│  ├─> ISkillLoader (5 skills)       │
│  ├─> IConversationStore (SQLite)   │
│  └─> IAgentProvider (Ollama)        │
│      └─> http://localhost:11434    │
└─────────────────────────────────────┘
               │
┌──────────────▼──────────────────────┐
│  .NET Aspire Dashboard (:15888)     │
│  Logs | Traces | Metrics | Health   │
└─────────────────────────────────────┘
```

---

## Configuration Override (without editing files)

```powershell
# Use Foundry Local instead of Ollama
$env:Model__Provider  = "foundry-local"
$env:Model__Model     = "phi-4"
aspire start src\OpenClawNet.AppHost

# Or use Azure OpenAI
$env:Model__Provider  = "azure-openai"
$env:Model__Model     = "gpt-4o-mini"
$env:Model__Endpoint  = "https://my-resource.openai.azure.com/"
$env:Model__ApiKey    = "your-key"
aspire start src\OpenClawNet.AppHost
```

---

## You've Completed All Demos 🎉

| What you've seen | How |
|---|---|
| Direct LLM call | Demo 01–02 |
| Multi-turn history | Demo 03 |
| Tool calling (FileSystem, Shell) | Demo 04 |
| Skill-driven personas | Demo 05 |
| Real-time token streaming | Demo 06 |
| Provider portability | Demo 07 |
| Event-driven agent runs | Demo 08 |
| Full production stack | Demo 09 |

The full platform is ~4,500 lines of .NET 10 across 27 projects. Each layer is independently testable, configurable, and extensible.
