# Demo 01 — Launch OpenClaw .NET & Dashboard Tour

**Level:** 🟢 Beginner | **Time:** ~5 min  
**Shows:** Starting the full Aspire stack, Aspire Dashboard navigation, service health

---

## What You'll See

One command starts everything. The Aspire Dashboard gives you a real-time view of all running services, their health, logs, and configuration — before you write a single chat message.

---

## Step 1 — Start the Full Stack

```powershell
aspire start src\OpenClawNet.AppHost
```

> **Tip:** When prompted, select option **3** to run the App Host with Aspire dashboard integration.

Wait for the output:

```
Building...
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 13.2.2
info: Aspire.Hosting.DistributedApplication[0]
      Resources:
        gateway (http)  running  →  http://localhost:PORT
        web     (http)  running  →  http://localhost:PORT
info: Aspire.Hosting.DistributedApplication[0]
      Login to the dashboard at:
      http://localhost:15888/login?t=<token>
```

Click the dashboard link (or copy it to the browser).

---

## Step 2 — Dashboard: Resources Tab

The **Resources** tab is the home screen. You'll see:

| Resource | Type | Status | URL |
|----------|------|--------|-----|
| `openclawnet-db` | SQLite | ✅ Ready | (file resource) |
| `openclawnet-db-sqliteweb` | Container | ✅ Running | http://localhost:PORT |
| `gateway` | Project | ✅ Running | http://localhost:PORT |
| `web` | Project | ✅ Running | http://localhost:PORT |

Both show green health indicators after their `/health` endpoints respond.

**Copy both URLs** — you'll use them throughout all demos:
```powershell
$gateway = "http://localhost:GATEWAY-PORT"
$web     = "http://localhost:WEB-PORT"
```

Click on the `gateway` resource row to see its **Parameters tab** — the Ollama endpoint, model, and connection string are shown there.

---

## Step 3 — Dashboard: Logs Tab

Click the **Console** icon next to `gateway` to see its live structured logs:

```
info: OpenClawNet.Models.Ollama.OllamaModelClient[0]
      Ollama provider ready. Endpoint: http://localhost:11434 Model: gemma4:e2b
info: OpenClawNet.Gateway Endpoints registered:
      /api/chat /api/sessions /api/tools /api/skills /api/webhooks /api/chat/stream
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:PORT
```

These are structured logs — use the filter box to narrow by category, e.g. type `IAgentProvider` to see only model provider messages.

---

## Step 4 — Verify Health Endpoints

```powershell
Invoke-RestMethod "$gateway/health"
```

```json
{ "status": "healthy", "timestamp": "2026-04-13T18:00:00Z" }
```

```powershell
Invoke-RestMethod "$gateway/api/version"
```

```json
{ "version": "0.1.0", "name": "OpenClawNet" }
```

---

## Step 5 — Open the Web UI

Navigate to the `web` URL in your browser. You'll see:

- **Sidebar**: empty sessions list (for now)  
- **Main area**: welcome screen with a message input  
- **Top bar**: "OpenClawNet" branding

The Web UI is a Blazor Server app connected to the Gateway via its Aspire-injected service reference. It uses HTTP SSE streaming (`POST /api/chat/stream`) for real-time token delivery. The toolbar at the top shows provider, model, active jobs, and connection status.

---

## Step 6 — Aspire 13.2 Feature: Connect GitHub Copilot CLI via MCP

Aspire 13.1 introduced `aspire agent init` which configures MCP so AI coding assistants can see your running Aspire app's state:

```powershell
aspire agent init
```

Select **Configure GitHub Copilot CLI to use Aspire MCP server**.

Once configured, you can ask GitHub Copilot CLI:
```
@workspace what services are running in my Aspire app?
```

And Copilot will query your live dashboard for real-time resource state.

---

## What Aspire Is Doing

```
AppHost.cs
  var sqlite = builder.AddSqlite("openclawnet-db", dbPath, "openclawnet.db")
    .WithSqliteWeb()                   ← adds SQLite Web browser UI to the dashboard

  builder.AddProject<OpenClawNet_Gateway>("gateway")
    .WithExternalHttpEndpoints()       ← expose on random port
    .WithHttpHealthCheck("/health")    ← poll /health every 5s
    .WithReference(sqlite)             ← inject DB connection string into Gateway
    .WaitFor(sqlite)                   ← Gateway won't start until SQLite is ready

  builder.AddProject<OpenClawNet_Web>("web")
    .WithReference(gateway)            ← inject gateway URL into Web
    .WaitFor(gateway)                  ← Web won't start until gateway is healthy
```

`WaitFor` means even if you refresh too fast, the Web UI won't start until the Gateway passes its health check. That's orchestration, not luck.

### SQLite Web UI

The Aspire Dashboard also shows a **`openclawnet-db-sqliteweb`** resource — click its URL to open a browser-based SQLite admin where you can browse the `Sessions`, `Messages`, and `ScheduledJobs` tables in real time.

---

## Next

→ **[Demo 02 — First Chat](demo-02-first-chat.md)**: open the Web UI and send your first message.
