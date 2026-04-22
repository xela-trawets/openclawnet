# How We Built OpenClawNet with GitHub Copilot CLI + Squad

We took the OpenClawNet concept — a .NET 10 AI agent framework demo app — from idea to **production-ready 4-session live learning series** using GitHub Copilot CLI and an AI team called Squad. The result: 2 repos, 4 complete sessions, staged code, bilingual materials, slides, and promotion content. Here's what that actually means.

## What Got Built

This isn't a "we used AI" story. This is a "we shipped a real training series" story. The deliverables:

### 📚 **Learning Materials**
- **4 comprehensive session guides** (EN + ES) — full presenter scripts, timing breakdowns, 12 Copilot prompts per session with expected outputs, troubleshooting, testing steps
- **Attendee setup guides** — local dev environment, prerequisites, platform-specific instructions
- **Demo scripts** — live coding prompts validated against actual Copilot Chat output
- **Copilot prompt catalogs** — 48 total prompts (12 per session) for progressive skill-building

### 💻 **Code & Staging**
- **Incremental code overlays** — `scripts/stages/session-{1-4}/` contains staged solution files so each session builds on the last
- **Working app** — .NET 10, Blazor UI, Aspire orchestration, SQLite/EF Core, Ollama + Azure OpenAI support
- **Migration path** — Session 1 delivers local chat in 60 minutes; Session 4 deploys to Azure with Foundry agents

### 🎤 **Presentations**
- **reveal.js slide decks** — 4 full sessions with OpenClawNet branding, speaker notes, progressive disclosure (Fragments API)
- **HTML build system** — presentations as code (`docs/presentations/`)
- **Speaker-ready** — timer, current/next slide preview, static output (no runtime server needed)

### 📣 **Promotion Content**
- **Social media posts** — LinkedIn (EN + ES), Twitter/X (EN + ES)
- **Image generation prompts** — FLUX.2 Pro prompts for social cards and visual assets
- **Platform-optimized** — character counts, hashtags, call-to-action for Microsoft Reactor series

### 📋 **Planning & Decisions**
- **Architecture decision log** — `.squad/decisions.md` tracks 6 major decisions (session restructure, reveal.js choice, cascade update rule, reviews)
- **Resource catalog** — `.squad/resources.md` with official URLs, branding rules (Aspire, not .NET Aspire), tech stack
- **Quality reviews** — Ripley (architect) reviewed Session 1 for code alignment; Parker (docs) assessed readiness at 7/10 with actionable fixes

### 🔄 **Two-Repo Workflow**
- **Private planning repo** (`elbruno/openclawnet-plan`) — Squad workspace, all materials, incremental staging
- **Public attendee repo** (`elbruno/openclawnet`) — receives session-specific materials via `prepare-session.ps1`
- **Controlled release** — attendees get exactly what they need per session, no spoilers for future weeks

## How Squad Works

Squad is an AI team orchestration system inside `.squad/`. The team:

- **Ripley** (Lead/Architect) — architecture, session design, code review
- **Dallas** (Backend) — Gateway API, model clients, agent runtime
- **Lambert** (Frontend) — Blazor UI, HTTP SSE streaming
- **Ash** (Tester) — validation, test prompts
- **Bishop** (DevOps) — Aspire orchestration, cloud deployment
- **Parker** (Docs/DevRel) — guides, demos, bilingual content
- **Brett** (Presentations) — slides, speaker notes, HTML builds

Each agent has a charter (`.squad/agents/{name}/charter.md`), decision-making authority, and persistent memory. They collaborate via a decision log, ceremonies (standups, retrospectives), and routing rules. GitHub Copilot CLI is the orchestration engine.

## What This Demonstrates

1. **Real output, fast** — Not prototypes. Production-ready materials for a 4-week series, built in weeks not months.
2. **AI agents as colleagues** — Squad members own domains, make decisions, review each other's work. Ripley caught 6 categories of guide-to-code drift in Session 1. Parker rated readiness and prioritized 9 fixes.
3. **Multi-lingual by default** — Spanish translations maintained alongside English, with human review workflow.
4. **Documentation as code** — Presentations, guides, and demos all versioned in Git, reproducible builds.
5. **Incremental teaching** — Staged code means Session 1 attendees see foundation work; Session 4 attendees see cloud deployment — same repo, different checkpoints.

## Stack

.NET 10 | C# | Blazor | Aspire | SQLite + EF Core | Ollama | Azure OpenAI | Microsoft Foundry | reveal.js

## Try It

- **Public repo:** https://github.com/elbruno/openclawnet
- **Presentations:** `docs/presentations/` (markdown → HTML)
- **Squad workspace:** `.squad/` (read `decisions.md`, `resources.md`, `roster.md`)

Built by Bruno Capuano with GitHub Copilot CLI + Squad.  
**If it's not documented, it doesn't exist. If it can't be demoed, it's not ready.** — Parker
