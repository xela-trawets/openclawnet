# Demo 06 — Provider Switch

**Level:** 🔴 Advanced | **Time:** ~5 min  
**Shows:** Same OpenClawNet stack, different LLM underneath — config-only swap

---

## Prerequisites

- OpenClawNet running via AppHost
- At least one provider installed/configured
- Gateway URL: `$gateway = "http://localhost:PORT"`

---

## What You'll See

The `IAgentProvider` abstraction means the entire agent stack — tools, skills, streaming, history, tracing — works identically regardless of which LLM is underneath. Switch provider by changing one config value or using the **Model Providers** page. No code changes.

OpenClawNet now supports **6 providers** and lets you manage them through a dedicated **Model Providers** page with CRUD, test, and enable/disable. You can also create **Agent Profiles** that bind a provider + model + instructions into a reusable configuration.

---

## Supported Providers

| Config value | Provider | Recommended model |
|---|---|---|
| `ollama` | Local Ollama | `gemma4:e2b` ✨ or `llama3.2` |
| `foundry-local` | Foundry Local (AI Toolkit) | `phi-4` |
| `azure-openai` | Azure OpenAI | `gpt-4o-mini` |
| `foundry` | Microsoft Foundry | (varies) |
| `github-copilot` | GitHub Copilot SDK | (varies) |

---

## Option 1 — Per-Request Override (no restart)

Specify provider and model on any single request:

```powershell
$s = (Invoke-RestMethod "$gateway/api/sessions" -Method POST `
    -ContentType "application/json" -Body '{"title": "Provider comparison"}').id

# gemma4:e2b via Ollama
$r1 = Invoke-RestMethod "$gateway/api/chat" -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s; message="What is 17 * 23?"; provider="ollama"; model="gemma4:e2b" } | ConvertTo-Json)

# llama3.2 via Ollama
$r2 = Invoke-RestMethod "$gateway/api/chat" -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s; message="What is 17 * 23?"; provider="ollama"; model="llama3.2" } | ConvertTo-Json)

Write-Host "gemma4:e2b: $($r1.content)"
Write-Host "llama3.2:  $($r2.content)"
```

Both go through the same agent stack — only the `IAgentProvider` implementation differs.

---

## Option 2 — Model Providers Page (Web UI — no restart)

The easiest way to switch providers is through the **Model Providers** page in the Web UI:

1. Open the Web UI → **Settings** → **Model Providers**
2. Click **Add Provider** or edit an existing configuration
3. Fill in provider type, name, endpoint, model, and credentials
4. Click **Test** to verify connectivity
5. **Enable** the provider — it becomes the active provider for new requests

You can configure multiple providers simultaneously (e.g., Ollama AND Azure OpenAI) and switch between them without losing settings.

### Agent Profiles

For per-session or per-job control, use **Agent Profiles** (Settings → Agent Profiles):

- Create named profiles (e.g., "Fast Local", "Cloud Premium") each referencing a different provider
- The default profile is "OpenClawNet Agent"
- Scheduled jobs use `AgentProfileName` to control which provider and instructions they use

---

## Option 3 — Edit appsettings.json (restart required)

For headless environments, edit `src/OpenClawNet.Gateway/appsettings.json`:

### Ollama with gemma4:e2b (recommended)
```json
"Model": {
  "Provider": "ollama",
  "Model": "gemma4:e2b",
  "Endpoint": "http://localhost:11434"
}
```

Pull first: `ollama pull gemma4:e2b`

> **Why gemma4:e2b?** Native function calling, 128K context window, edge-optimised.
> Tool decisions are more reliable and the reasoning loop is faster per iteration.

### Ollama with llama3.2 (default)
```json
"Model": {
  "Provider": "ollama",
  "Model": "llama3.2",
  "Endpoint": "http://localhost:11434"
}
```

### Foundry Local (phi-4)
```json
"Model": {
  "Provider": "foundry-local",
  "Model": "phi-4"
}
```

Install: `winget install Microsoft.AIToolkit` then `foundry-local start phi-4`

### Azure OpenAI
```json
"Model": {
  "Provider": "azure-openai",
  "Model": "gpt-4o-mini"
}
```

Set credentials (user secrets — never in appsettings):
```powershell
cd src/OpenClawNet.Gateway
dotnet user-secrets set "Model:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
dotnet user-secrets set "Model:ApiKey"   "YOUR-API-KEY"
```

---

## Option 4 — Environment Variables

```powershell
$env:Model__Provider  = "ollama"
$env:Model__Model     = "gemma4:e2b"
aspire start src\OpenClawNet.AppHost
```

---

## Compare in the Aspire Dashboard

Run the same query twice with different models, then open **Traces**:

```powershell
# gemma4:e2b request
$s1 = (Invoke-RestMethod "$gateway/api/sessions" -Method POST -ContentType "application/json" -Body '{}').id
Invoke-RestMethod "$gateway/api/chat" -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s1; message="Explain async/await in 2 sentences."; model="gemma4:e2b" } | ConvertTo-Json) | Out-Null

# llama3.2 request
$s2 = (Invoke-RestMethod "$gateway/api/sessions" -Method POST -ContentType "application/json" -Body '{}').id
Invoke-RestMethod "$gateway/api/chat" -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s2; message="Explain async/await in 2 sentences."; model="llama3.2" } | ConvertTo-Json) | Out-Null
```

In **Traces**, compare the `gen_ai.chat` span durations for each request. The GenAI visualizer shows which model processed each one.

---

## How the Switch Works

All 6 providers implement `IAgentProvider`. The `RuntimeAgentProvider` routes requests to the correct concrete provider based on the active `ModelProviderDefinition` or per-request override.

```
IAgentProvider (abstraction)
  ├─ OllamaAgentProvider
  ├─ AzureOpenAIAgentProvider
  ├─ FoundryAgentProvider
  ├─ FoundryLocalAgentProvider
  ├─ GitHubCopilotAgentProvider
  └─ RuntimeAgentProvider (router)
```

Provider configurations are managed via the `/api/model-providers` API (CRUD + test + enable/disable) and the Model Providers UI page. `IAgentProvider` is resolved everywhere by DI. Nothing else in the stack knows which concrete provider is active.

---

## Next

→ **[Demo 07 — Dashboard Deep-Dive](demo-07-dashboard.md)**: explore the Aspire Dashboard's GenAI visualizer, structured logs, and traces in depth.
