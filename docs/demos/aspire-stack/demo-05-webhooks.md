# Demo 05 — Event-Driven Webhooks

**Level:** 🟡 Intermediate | **Time:** ~8 min  
**Shows:** `POST /api/webhooks/{eventType}`, agent triggered by external events, automatic session creation per event

---

## Prerequisites

- OpenClawNet running via AppHost
- Gateway URL: `$gateway = "http://localhost:PORT"` (from Aspire Dashboard)
- Recommended model: `gemma4:e2b` or `llama3.2`

---

## What You'll See

Instead of a user typing a message, an external system fires an HTTP event. The Gateway creates a new isolated session, converts the payload to a natural-language prompt, and runs the full agent pipeline. Each event gets its own auditable trace in the Aspire Dashboard.

---

## Step 1 — Simple Custom Event

```powershell
$r = Invoke-RestMethod "$gateway/api/webhooks/deploy-complete" `
    -Method POST -ContentType "application/json" `
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
  "agentResponse": "Great news! Version 2.4.1 has been successfully deployed to staging in 42 seconds.",
  "toolCallCount": 0
}
```

---

## Step 2 — Event That Triggers Tool Use

Send an event that causes the agent to read project files:

```powershell
$r = Invoke-RestMethod "$gateway/api/webhooks/code-review-request" `
    -Method POST -ContentType "application/json" `
    -Body (@{
        message = "A pull request was opened. Review the Gateway project."
        data    = @{
            author  = "alice"
            scope   = "src/OpenClawNet.Gateway"
            pr_id   = 42
        }
    } | ConvertTo-Json -Depth 3)

Write-Host "Tools called: $($r.toolCallCount)"
$r.agentResponse
```

The agent lists and reads files in `src/OpenClawNet.Gateway`, then produces a code review. Watch the Aspire Dashboard → Traces for the full tool call waterfall.

---

## Step 3 — List All Webhook Sessions

```powershell
Invoke-RestMethod "$gateway/api/webhooks" | ConvertTo-Json -Depth 2
```

Each entry is a session created by a webhook event:
```json
[
  { "id": "...", "title": "Webhook: deploy-complete @ 2026-04-13 18:00Z", "createdAt": "..." },
  { "id": "...", "title": "Webhook: code-review-request @ 2026-04-13 18:01Z", "createdAt": "..." }
]
```

---

## Step 4 — Inspect a Webhook Session Trace

Copy a session ID from the list above and read its full message history:

```powershell
$sessionId = "PASTE-SESSION-ID"
Invoke-RestMethod "$gateway/api/sessions/$sessionId/messages" |
    ForEach-Object {
        Write-Host "[$($_.role.ToUpper())]"
        Write-Host ($_.content.Substring(0, [Math]::Min(200, $_.content.Length)))
        Write-Host "---"
    }
```

You'll see the auto-generated user message from the webhook payload, followed by the agent's tool calls and final response.

---

## Step 5 — Simulate a GitHub Push Event

```powershell
$r = Invoke-RestMethod "$gateway/api/webhooks/github-push" `
    -Method POST -ContentType "application/json" `
    -Body (@{
        message = "New code was pushed to main. Analyse the changes."
        data    = @{
            ref        = "refs/heads/main"
            commits    = @(
                @{
                    id      = "abc123"
                    message = "Add native tool calling to OllamaModelClient"
                    added   = @("src/OpenClawNet.Models.Ollama/OllamaModelClient.cs")
                }
            )
            repository = @{ name = "openclawnet" }
        }
    } | ConvertTo-Json -Depth 5)

$r.agentResponse
```

---

## Step 6 — View in Aspire Dashboard

After firing a few webhooks, open the Aspire Dashboard → **Traces** tab. Filter by resource `gateway`. You'll see each webhook as its own trace, with tool spans inside.

Each webhook is independent — no shared state, clean separation, full audit trail.

---

## How It Works

```
POST /api/webhooks/github-push  { message, data }
  └─> WebhookEndpoints
      ├─> CREATE ChatSession (provider="webhook", title="Webhook: github-push @ ...")
      ├─> Build UserMessage from payload:
      │     "A 'github-push' webhook event was received.
      │      Process it and take appropriate action.
      │      Payload: { "ref": "refs/heads/main", ... }"
      ├─> IAgentOrchestrator.ProcessAsync()  ← full agent loop, tools, skills
      └─> return { sessionId, eventType, agentResponse, toolCallCount }
```

---

## Next

→ **[Demo 06 — Provider Switch](demo-06-provider-switch.md)**: swap the LLM with a config change only.
