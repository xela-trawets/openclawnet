# 🔧 Session 2: Tools + Agent Workflows

![Duration](https://img.shields.io/badge/Duration-50%20min-blue)
![Level](https://img.shields.io/badge/Level-Intermediate%20.NET-purple)
![Session](https://img.shields.io/badge/Session-2%20of%204-green)

## Overview

Turn a **chatbot** into an **agent**. A chatbot generates text; an agent **uses tools** to interact with the world — reading files, running commands, fetching web pages, scheduling tasks. This session builds the complete tool framework with defense-in-depth security at every layer.

## Prerequisites

- ✅ Session 1 complete and working
- ✅ Local LLM running (Ollama with `llama3.2` or Foundry Local)
- ✅ .NET 10 SDK, VS Code or Visual Studio
- ✅ Understanding of: interfaces, DI, async/await

## What You'll Learn

### 🔧 Stage 1: Tool Architecture (12 min)
- `ITool` interface — the contract every tool implements
- `IToolRegistry` (discovery) vs `IToolExecutor` (safe execution)
- `IToolApprovalPolicy` — security gate for dangerous operations
- `ToolMetadata`, `ToolInput`, `ToolResult` — the abstraction layer

### 🛡️ Stage 2: Built-in Tools + Security (15 min)
- **FileSystemTool** — path traversal prevention with `Path.GetFullPath`
- **ShellTool** — command blocklist, 30s timeout, approval required
- **WebTool** — SSRF protection with private IP blocklist
- **SchedulerTool** — cron-based job CRUD with EF Core persistence
- Three real-world attack demos (path traversal, command injection, SSRF)

### 🔄 Stage 3: Agent Loop + Integration (15 min)
- The agent reasoning loop: prompt → model → tool calls → execute → loop
- `AgentOrchestrator` and `DefaultAgentRuntime` — the core algorithm
- `DefaultPromptComposer` — how tools get injected into the system prompt
- Safety limit: `MaxToolIterations = 10`

## Projects Covered

| Project | LOC | Key Responsibility |
|---------|-----|-------------------|
| OpenClawNet.Tools.Abstractions | 90 | ITool, IToolExecutor, IToolRegistry, ToolMetadata |
| OpenClawNet.Tools.Core | 101 | ToolExecutor, ToolRegistry, DI extensions |
| OpenClawNet.Tools.FileSystem | 142 | File read/write/list with path validation |
| OpenClawNet.Tools.Shell | 148 | Command execution with blocklist + timeout |
| OpenClawNet.Tools.Web | 121 | HTTP fetch with SSRF protection |
| OpenClawNet.Tools.Scheduler | 173 | Cron-based job scheduling |
| OpenClawNet.Agent | — | AgentOrchestrator, DefaultAgentRuntime, PromptComposer |

## Session Materials

| Resource | Link |
|----------|------|
| 📖 Presenter Guide | [session-2-guide.md](../session-2-guide.md) |
| 📖 Guía (Español) | [session-2-guide-es.md](../session-2-guide-es.md) |
| 🎤 Speaker Script | [speaker-script.md](./speaker-script.md) |
| 🤖 Copilot Prompts | [copilot-prompts.md](./copilot-prompts.md) |
| 🖥️ Slides | [slides.md](./slides.md) |

## Git Checkpoints

- **Starting tag:** `session-2-start` (alias: `session-1-complete`)
- **Ending tag:** `session-2-complete`

## Security Recap

| Threat | Tool | Defense |
|--------|------|---------|
| Path Traversal | FileSystemTool | `Path.GetFullPath` + workspace boundary check |
| Command Injection | ShellTool | Blocked commands HashSet + 30s timeout |
| SSRF | WebTool | Private IP blocklist + scheme validation |

> **Pattern:** Validate inputs *before* execution. Fail fast. Fail safe.
