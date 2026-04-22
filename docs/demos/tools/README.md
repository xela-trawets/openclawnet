# 🛠️ Tools demos

Step-by-step walkthroughs that show how to combine OpenClaw .NET's **built-in tools** into useful agent runs and scheduled jobs.

These demos are deliberately small, recipe-style guides — pick one, follow the steps, and you'll have a working scenario in 10–20 minutes. They focus on the **wiring** (which tools, in what order, with what prompt), not on building new code.

> Looking for the full tool reference? See **[`docs/manuals/20-tools.md`](../../manuals/20-tools.md)**.
> Looking for the bigger end-to-end scenarios? See **[`real-world/`](../real-world/README.md)**.

> 🎯 **Quick start:** every demo on this page is also shipped as a **built-in [job template](../../manuals/30-jobs.md#job-templates)**. Open the Web UI → **Jobs → Job Templates** to seed any of them with one click.

## Demos in this track

| # | Demo | Surface | Tools used |
| --- | --- | --- | --- |
| [01](./01-watched-folder-summarizer/README.md) | **Watched folder → markdown → summary** (scheduled job, every 5 min) | Jobs page | `file_system`, `markdown_convert` (+ agent summarization) |
| [02](./02-github-issue-triage/README.md) | **GitHub issue triage** (one-shot agent run) | Chat / Agent Probe | `github`, `html_query` |
| [03](./03-research-and-archive/README.md) | **Research & archive** (web → markdown → local semantic index) | Chat | `markdown_convert`, `file_system`, `embeddings` |
| [04](./04-image-batch-resize/README.md) | **Image batch resize** (hourly job, folder → WebP thumbnails) | Jobs page | `file_system`, `image_edit` |
| [05](./05-text-to-speech-snippet/README.md) | **Text → speech snippet** (one chat turn produces a WAV) | Chat / Direct Invoke | `text_to_speech` |

More demos will be added as new tools land. Suggestions and PRs welcome.

## Prerequisites (all demos)

- OpenClaw .NET running locally (`aspire start src\OpenClawNet.AppHost`) — see **[01-local-installation.md](../../manuals/01-local-installation.md)**.
- A model provider configured (Ollama, Azure OpenAI, GitHub Copilot, or Foundry).
- For demo 1: a folder you can write into (we'll use `c:\temp\sampleDocs`).
- For demo 2: optional `GITHUB_TOKEN` secret if you want to avoid GitHub's anonymous rate limit (see the demo for setup).

## How these demos are organized

Each subfolder contains:

- `README.md` — the full step-by-step guide (UI-first, with a copy-paste API alternative at the end).
- Optional `*.json` payloads or screenshots when they help.

You can read them in any order; they don't depend on each other.
