# OpenClaw .NET — User Manuals

Welcome to the OpenClaw .NET user documentation. These manuals walk you from a fresh machine all the way to running scheduled, tool-calling agents locally.

Read them in order the first time you set up the project; afterwards each page works as a standalone reference.

---

## Manuals

| # | Manual | What you will learn |
|---|--------|---------------------|
| 00 | **[Prerequisites](./00-prerequisites.md)** | Hardware sizing and the software you need to install (.NET 10 SDK, Aspire CLI, Docker Desktop, Ollama, Git, an editor). |
| 01 | **[Local Installation](./01-local-installation.md)** | Clone the repo, restore, build, and launch the full Aspire orchestration with `aspire start`. Verify the dashboard, Web UI, and SQLite admin. |
| 02 | **[Hello World](./02-hello-world.md)** | Create your first custom agent profile, chat with it, and (optionally) schedule a job. A hands-on 10-minute walkthrough for first-time users. |
| 10 | **[Settings](./10-settings.md)** | Configure model providers, the Scheduler, tools, and memory from the Web UI. Where settings live and how precedence works. |
| 20 | **[Tools](./20-tools.md)** | Reference for the built-in tools (`file_system`, `shell`, `web_fetch`, `schedule`) — parameters, examples, and safety notes. |
| 30 | **[Jobs](./30-jobs.md)** | Create, schedule, monitor, and troubleshoot jobs. Cron quick reference, one-shot jobs, retries, and templates. |

---

## Recommended Reading Order

```mermaid
flowchart LR
  A[00 Prerequisites] --> B[01 Local Installation]
  B --> C[02 Hello World]
  C --> D[10 Settings]
  D --> E[20 Tools]
  E --> F[30 Jobs]
```

1. Start with **[00-prerequisites.md](./00-prerequisites.md)** to ensure your machine is ready.
2. Follow **[01-local-installation.md](./01-local-installation.md)** to launch the app.
3. Try **[02-hello-world.md](./02-hello-world.md)** for a hands-on first agent tutorial.
4. Open **[10-settings.md](./10-settings.md)** to pick your model provider and tune the scheduler.
5. Skim **[20-tools.md](./20-tools.md)** so you know what the agent can do.
6. Use **[30-jobs.md](./30-jobs.md)** to automate recurring or deferred tasks.

---

## Quick Reference

| You want to... | Go to |
|----------------|-------|
| Install .NET 10 / Aspire CLI / Docker / Ollama | [00-prerequisites.md](./00-prerequisites.md) |
| Run the app for the first time | [01-local-installation.md § 5](./01-local-installation.md#5-launch-with-the-aspire-apphost) |
| Create your first custom agent | [02-hello-world.md](./02-hello-world.md) |
| Switch from Ollama to Azure OpenAI | [10-settings.md § Model Settings](./10-settings.md#model-settings) |
| Disable a risky tool | [10-settings.md § Tools Settings](./10-settings.md#tools-settings) |
| Understand what `web_fetch` does | [20-tools.md § web_fetch](./20-tools.md#web_fetch) |
| Schedule a daily summary | [30-jobs.md § Examples](./30-jobs.md#examples) |
| Look up a cron expression | [30-jobs.md § Cron Expressions](./30-jobs.md#cron-expressions) |
| Reset the local database | [01-local-installation.md § Troubleshooting](./01-local-installation.md#troubleshooting) |

---

## Conventions

- **Code blocks** use language hints (`bash`, `powershell`, `json`, `http`) so they render with syntax highlighting.
- **Bash examples** work on macOS and Linux; on Windows use PowerShell 7+ — both shells run the same `dotnet` and `aspire` commands.
- **All times are UTC** unless stated otherwise.
- **Workspace-relative paths** look like `src/OpenClawNet.Gateway/appsettings.json` (no leading slash).

---

## Where to Get Help

- **Architecture deep dives:** [`docs/architecture/`](../architecture/)
- **Setup guides (alternative format):** [`docs/setup/`](../setup/)
- **Issues & questions:** [github.com/elbruno/openclawnet-plan/issues](https://github.com/elbruno/openclawnet-plan/issues)
- **Public attendee repo:** [github.com/elbruno/openclawnet](https://github.com/elbruno/openclawnet)

---

## License

[MIT](../../LICENSE)
