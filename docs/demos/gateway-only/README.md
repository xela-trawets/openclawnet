# Gateway-Only Demos

Standalone API demos — only the Gateway is running, no Web UI or Aspire needed.
Great for exploring individual features one at a time.

**Start with:**
```powershell
dotnet run --project src/OpenClawNet.Gateway
# Swagger UI: http://localhost:5010/swagger
```

Set your Gateway URL once and reuse it across demos:
```powershell
$gateway = "http://localhost:5010"
```

---

## Demo Index

| # | Demo | Level | What you'll see |
|---|------|-------|-----------------|
| [01](demo-01-hello-world.md) | Hello World | 🟢 Beginner | Health check, version info, OpenAPI spec |
| [02](demo-02-first-chat.md) | First Chat | 🟢 Beginner | `POST /api/chat`, single-turn LLM call through the full agent stack |
| [03](demo-03-multi-turn.md) | Multi-Turn Conversation | 🟡 Intermediate | Session management, message history, conversation continuity |
| [04](demo-04-tool-use.md) | Tool Use | 🟡 Intermediate | Agent decides to call FileSystem and Shell tools, incorporates results |
| [05](demo-05-skills.md) | Skills & Personas | 🟡 Intermediate | `ISkillLoader`, enabling/disabling skills, how skills change agent behaviour |
| [06](demo-06-streaming.md) | Real-Time Streaming | 🟡 Intermediate | `POST /api/chat/stream` (HTTP SSE/NDJSON), token-by-token streaming, tool call events |
| [07](demo-07-provider-switch.md) | Provider Switch | 🟡 Intermediate | Same API, different LLM — Ollama → Foundry Local → Azure OpenAI |
| [08](demo-08-webhooks.md) | Event-Driven Webhooks | 🔴 Advanced | `POST /api/webhooks/{eventType}`, agent triggered by external events |
| [09](demo-09-full-stack.md) | Full Aspire Stack | 🔴 Advanced | The full solution with Aspire orchestration (stepping stone to Aspire demos) |

---

## Recommended order

Work top to bottom — each demo builds on concepts introduced in the previous one.

Once you've finished these, head to the **[Aspire Stack demos](../aspire-stack/README.md)** for the full observability story with the Aspire Dashboard.

→ **[Back to demo index](../README.md)**
