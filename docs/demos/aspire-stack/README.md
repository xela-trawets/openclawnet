# Aspire Stack Demos

Full-solution demos — runs Gateway + Blazor Web UI + Aspire Dashboard together.

**Start with:**
```powershell
aspire start src\OpenClawNet.AppHost
```

Open the Aspire Dashboard at `http://localhost:15888`, then grab the Gateway and Web URLs from the Resources view.

---

## Demo Index

| # | Demo | Level | What you'll see |
|---|------|-------|-----------------|
| [01](demo-01-launch.md) | Launch & Dashboard Tour | 🟢 Beginner | Start the stack, navigate the Aspire Dashboard, MCP agent init |
| [02](demo-02-first-chat.md) | First Chat | 🟢 Beginner | Blazor Web UI, token streaming, conversation sessions |
| [03](demo-03-tool-tracing.md) | Tool Use & Tracing | 🟡 Intermediate | Agent calls tools, visualize the loop in the GenAI dashboard |
| [04](demo-04-skills.md) | Skills & Personas | 🟡 Intermediate | Enable skills at runtime, change agent behaviour |
| [05](demo-05-webhooks.md) | Event-Driven Webhooks | 🟡 Intermediate | Trigger agent from external events, session-per-event audit trail |
| [06](demo-06-provider-switch.md) | Provider Switch | 🔴 Advanced | Swap LLM provider with a config change only |
| [07](demo-07-dashboard.md) | Dashboard Deep-Dive | 🔴 Advanced | GenAI visualizer, distributed traces, structured logs, token metrics |

---

## Helper Variables (PowerShell)

```powershell
# Get these from the Aspire Dashboard → Resources
$gateway = "http://localhost:PORT"
$web     = "http://localhost:PORT"
```

---

## Recommended order

Run the demos top to bottom: each one builds on trace data or sessions created by the previous.
→ **[Back to demo index](../README.md)**
