# Demo 07 — Provider Switch

**Level:** 🟡 Intermediate | **Time:** ~5 min  
**Shows:** Same API, different LLM provider — Ollama → Foundry Local → Azure OpenAI

---

## What You'll See

The `IAgentProvider` abstraction means the entire agent stack — prompt composition, tool calling, streaming, history — works identically regardless of which LLM is underneath. Switching providers is a config-only change. No code changes.

OpenClawNet now supports **6 providers** and a dedicated **Model Providers** page in the Settings UI for managing them. You can also define **Agent Profiles** that bind a provider + model + system instructions into a named configuration.

---

## Provider Options

| Value | Provider | Model default | Requires |
|-------|----------|---------------|---------|
| `ollama` | Local Ollama | `llama3.2` | Ollama running |
| `foundry-local` | Foundry Local (AI Toolkit) | `phi-4` | NVIDIA/AMD GPU or CPU |
| `azure-openai` | Azure OpenAI | `gpt-4o-mini` | Azure subscription + user secrets |
| `foundry` | Microsoft Foundry | (varies) | Foundry endpoint + API key |
| `github-copilot` | GitHub Copilot SDK | (varies) | GitHub Copilot access |

---

## Option 1 — Per-Request Override (no restart)

You can specify provider and model on any individual request:

```powershell
$s = (Invoke-RestMethod http://localhost:5010/api/sessions -Method POST `
    -ContentType "application/json" -Body '{"title": "Provider comparison"}').id

# Default provider (ollama / llama3.2)
$r1 = Invoke-RestMethod http://localhost:5010/api/chat -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s; message="What is 17 * 23? Answer only with the number."; provider="ollama"; model="llama3.2" } | ConvertTo-Json)

# Foundry Local (phi-4) — if installed
$r2 = Invoke-RestMethod http://localhost:5010/api/chat -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s; message="What is 17 * 23? Answer only with the number."; provider="foundry-local"; model="phi-4" } | ConvertTo-Json)

Write-Host "Ollama says:        $($r1.content)"
Write-Host "Foundry Local says: $($r2.content)"
```

Both answers go through the same `IAgentOrchestrator`, same tool framework, same SQLite session — only the `IAgentProvider` implementation differs.

---

## Option 2 — Model Providers Page (UI — no restart)

The **Model Providers** page (Settings → Model Providers) lets you manage multiple named provider configurations through the web UI:

1. Open the Web UI → **Settings** → **Model Providers**
2. Click **Add Provider** to create a new provider configuration
3. Fill in the provider type, name, endpoint, model, and credentials
4. Use the **Test** button to verify connectivity
5. **Enable/disable** providers with the toggle — the active provider is used for new chat sessions

Each provider configuration is a `ModelProviderDefinition` entity stored in the database. You can have **multiple named configurations per provider type** (e.g., two different Azure OpenAI deployments).

### Agent Profiles

For more control, use **Agent Profiles** (Settings → Agent Profiles) to bind a provider + model + system instructions:

1. Open **Settings** → **Agent Profiles**
2. Create a named profile (e.g., "Code Reviewer") with provider reference, model, instructions, and tool selections
3. The default profile is "OpenClawNet Agent"
4. Scheduled jobs reference an `AgentProfileName` to control which provider and instructions they use

---

## Option 3 — appsettings.json (restart required)

For environments without the UI, edit `src/OpenClawNet.Gateway/appsettings.json`:

### Ollama (default)
```json
"Model": {
  "Provider": "ollama",
  "Model": "llama3.2",
  "Endpoint": "http://localhost:11434"
}
```

### Foundry Local (phi-4, runs on CPU or GPU)
```json
"Model": {
  "Provider": "foundry-local",
  "Model": "phi-4"
}
```

Install Foundry Local first:
```powershell
winget install Microsoft.AIToolkit
foundry-local start phi-4
```

### Azure OpenAI (requires subscription)
```json
"Model": {
  "Provider": "azure-openai",
  "Model": "gpt-4o-mini"
}
```

Set credentials via user secrets (never in appsettings):
```powershell
cd src/OpenClawNet.Gateway
dotnet user-secrets set "Model:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
dotnet user-secrets set "Model:ApiKey"   "YOUR-API-KEY"
```

---

## Option 4 — Environment Variables (CI/CD or Docker)

```powershell
$env:Model__Provider  = "azure-openai"
$env:Model__Model     = "gpt-4o-mini"
$env:Model__Endpoint  = "https://my-resource.openai.azure.com/"
$env:Model__ApiKey    = $env:AZURE_OPENAI_KEY

dotnet run --project src/OpenClawNet.Gateway
```

---

## Verifying the Active Provider

The `GET /api/version` endpoint doesn't expose the provider, but you can check from the logs when the Gateway starts:

```
info: OpenClawNet.Models.Ollama.OllamaModelClient[0]
      Ollama provider ready. Endpoint: http://localhost:11434, Model: llama3.2
```

Or ask the agent directly:
```powershell
$s = (Invoke-RestMethod http://localhost:5010/api/sessions -Method POST `
    -ContentType "application/json" -Body '{}').id
(Invoke-RestMethod http://localhost:5010/api/chat -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s; message="Which LLM model are you?" } | ConvertTo-Json)).content
```

---

## How It Works

All 6 providers implement the `IAgentProvider` interface. The `RuntimeAgentProvider` routes requests to the correct concrete provider based on the active `ModelProviderDefinition` or per-request override.

```
IAgentProvider (abstraction)
  ├─ OllamaAgentProvider
  ├─ AzureOpenAIAgentProvider
  ├─ FoundryAgentProvider
  ├─ FoundryLocalAgentProvider
  ├─ GitHubCopilotAgentProvider
  └─ RuntimeAgentProvider (router)
```

`IAgentProvider` is resolved by DI everywhere. The rest of the agent stack never knows which concrete provider is active.

Provider configurations are managed via the **Model Providers** API (`/api/model-providers`) with full CRUD, test, and enable/disable support.

---

## Adding a New Provider

1. Create a class implementing `IAgentProvider` in a new project
2. Register it in DI
3. Add a `ModelProviderDefinition` via the API or UI

That's it — tools, skills, memory, streaming, sessions all work automatically.

---

## Next

→ **[Demo 08 — Event-Driven Webhooks](demo-08-webhooks.md)**: trigger an agent run from an external event.
