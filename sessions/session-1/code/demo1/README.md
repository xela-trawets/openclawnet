# Demo 1 — Console Chat with IAgentProvider

A minimal .NET 10 console app that uses `IAgentProvider` to chat with a local Ollama model. Demonstrates the provider-agnostic architecture — switch models or providers by changing a few lines.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Ollama](https://ollama.com/) running locally with a model pulled (e.g., `ollama pull gemma4:e2b`)

## Run

```bash
cd OpenClawNet.Demo1.Console
dotnet run
```

## What It Shows

1. **IAgentProvider** — the abstraction every provider implements
2. **OllamaAgentProvider** — creates an `IChatClient` backed by a local Ollama instance
3. **IChatClient** — the Microsoft.Extensions.AI standard interface for chat completions
4. **Streaming** — tokens arrive one at a time via `GetStreamingResponseAsync()`

## Switching Providers

The code has commented-out blocks showing how to switch:

- **Different Ollama model** — change the model name (e.g., `phi4`, `llama3.2`)
- **Azure OpenAI** — uncomment the Azure section, set credentials via User Secrets:
  ```bash
  cd OpenClawNet.Demo1.Console
  dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
  dotnet user-secrets set "AzureOpenAI:ApiKey" "YOUR-KEY"
  dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-5-mini"
  ```
- **GitHub Copilot SDK** — uncomment the Copilot section. Auth options:
  ```bash
  # Option A: gh CLI login (simplest)
  gh auth login

  # Option B: token via User Secrets
  cd OpenClawNet.Demo1.Console
  dotnet user-secrets set "GitHubCopilot:GitHubToken" "YOUR-GITHUB-TOKEN"
  ```
  Default model is `gpt-5-mini`. Change via `GitHubCopilot:Model` (e.g., `gpt-5`, `claude-sonnet-4.5`).
  > Requires a [GitHub Copilot subscription](https://github.com/features/copilot#pricing) (free tier available).

## Architecture

```
IAgentProvider                    (abstraction)
  ├── OllamaAgentProvider         (local LLM via Ollama)
  ├── AzureOpenAIAgentProvider    (cloud LLM via Azure)
  └── GitHubCopilotAgentProvider  (GitHub Copilot SDK)
```

Each provider's `CreateChatClient(AgentProfile)` returns an `IChatClient` — the standard interface from `Microsoft.Extensions.AI`. Your app code never changes when you swap providers.
