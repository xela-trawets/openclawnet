# Demo 08 — Event-Driven Webhooks

**Level:** 🔴 Advanced | **Time:** ~8 min  
**Shows:** `POST /api/webhooks/{eventType}`, agent triggered by external events, automatic session creation

---

## Prerequisites

- Gateway running on `http://localhost:5010`
- Ollama running with `llama3.2` pulled
- For GitHub integration: public URL (use `ngrok` or similar tunnel)

---

## What You'll See

Instead of a user typing a message, an external system fires a webhook. The Gateway creates a new session, converts the event payload into a natural-language prompt, and runs the full agent pipeline. The agent can use tools to react — read files, execute commands, call APIs.

Each webhook gets its own auditable session in SQLite.

---

## Step 1 — Simple Custom Event

```powershell
$r = Invoke-RestMethod http://localhost:5010/api/webhooks/deploy-complete `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        message = "Deployment to staging completed successfully."
        data    = @{
            environment = "staging"
            version     = "2.4.1"
            duration_s  = 42
            status      = "success"
        }
    } | ConvertTo-Json -Depth 3)

$r | ConvertTo-Json
```

Expected:
```json
{
  "sessionId": "...",
  "eventType": "deploy-complete",
  "agentResponse": "Great news! Version 2.4.1 has been successfully deployed to staging in 42 seconds...",
  "toolCallCount": 0
}
```

The agent processed the event in its own session.

---

## Step 2 — Agent Reacts with Tools

Send an event that causes the agent to take action using the FileSystem tool:

```powershell
$r = Invoke-RestMethod http://localhost:5010/api/webhooks/code-review-request `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        message = "A developer requested a code review of the project."
        data    = @{
            requester = "alice"
            scope     = "src/OpenClawNet.Agent"
        }
    } | ConvertTo-Json -Depth 3)

Write-Host "Tools called: $($r.toolCallCount)"
Write-Host $r.agentResponse
```

The agent will call the `file_system` tool to list and read files in `src/OpenClawNet.Agent`, then provide a code review summary. Watch the Gateway logs — you'll see tool execution in real time.

---

## Step 3 — View Webhook Sessions

```powershell
Invoke-RestMethod http://localhost:5010/api/webhooks | ConvertTo-Json -Depth 3
```

Returns the last 20 webhook-triggered sessions with timestamps:
```json
[
  { "id": "...", "title": "Webhook: deploy-complete @ 2026-04-13 18:00:00Z", "createdAt": "..." },
  { "id": "...", "title": "Webhook: code-review-request @ 2026-04-13 18:01:00Z", "createdAt": "..." }
]
```

---

## Step 4 — Inspect the Full Agent Trace

Pick a session ID from the list above and inspect what the agent did:

```powershell
$sessionId = "PASTE-SESSION-ID-HERE"

Invoke-RestMethod "http://localhost:5010/api/sessions/$sessionId/messages" |
    ForEach-Object {
        Write-Host "[$($_.role.ToUpper())]"
        Write-Host ($_.content.Substring(0, [Math]::Min(200, $_.content.Length)))
        Write-Host "---"
    }
```

You'll see:
1. `[USER]` — the auto-generated message from the webhook payload
2. `[ASSISTANT]` — tool call decision
3. `[TOOL]` — tool result
4. `[ASSISTANT]` — final response

---

## Step 5 — Simulate a GitHub Push Event

Real GitHub webhooks send a specific payload format. Here's how to simulate one:

```powershell
$r = Invoke-RestMethod http://localhost:5010/api/webhooks/github-push `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{
        message = "New code was pushed to the main branch. Analyze the changes."
        data    = @{
            ref        = "refs/heads/main"
            commits    = @(
                @{
                    id      = "abc123"
                    message = "Add NDJSON streaming to ChatStreamEndpoints"
                    author  = @{ name = "Bruno"; email = "bruno@example.com" }
                    added   = @("src/OpenClawNet.Gateway/Endpoints/ChatStreamEndpoints.cs")
                    modified = @()
                }
            )
            repository = @{ name = "openclawnet"; full_name = "org/openclawnet" }
        }
    } | ConvertTo-Json -Depth 5)

$r.agentResponse
```

---

## Step 6 — Real GitHub Webhooks (optional)

To receive real GitHub webhook events:

1. Expose the Gateway publicly (development):
   ```powershell
   winget install ngrok.ngrok
   ngrok http 5010
   # Note the https://xxxx.ngrok.io URL
   ```

2. In GitHub repo → Settings → Webhooks → Add webhook:
   - Payload URL: `https://xxxx.ngrok.io/api/webhooks/github-push`
   - Content type: `application/json`
   - Events: Push, Pull request

3. Every push now triggers an agent run. Check sessions:
   ```powershell
   Invoke-RestMethod http://localhost:5010/api/webhooks
   ```

---

## How It Works

```
POST /api/webhooks/deploy-complete  { message, data }
  └─> WebhookEndpoints
      ├─> CREATE new ChatSession (provider="webhook")
      ├─> Build UserMessage:
      │   "A 'deploy-complete' webhook event was received.
      │    Process it and take appropriate action.
      │    Payload: { "environment": "staging", ... }"
      ├─> IAgentOrchestrator.ProcessAsync()
      │   └─> full tool-calling loop (same as /api/chat)
      └─> return WebhookResponse { sessionId, eventType, agentResponse, toolCallCount }
```

Every webhook fires its own session — complete audit trail, no shared state.

---

## Next

→ **[Demo 09 — Full Aspire Stack](demo-09-full-stack.md)**: run everything together with the Blazor web UI.
