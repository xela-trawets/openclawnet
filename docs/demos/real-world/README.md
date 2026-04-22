# 🎯 Real-World Scenarios — Advanced Demos

Welcome to the real-world demo series. These are **production-inspired use cases** that show how OpenClaw .NET handles advanced patterns beyond basic tutorials.

Each scenario is **20–30 minutes**, exercises **3+ framework features**, and bridges the gap between learning and production systems.

## Prerequisites

Before starting any real-world scenario:

- ✅ **OpenClaw .NET running** — Either [Aspire Stack](../aspire-stack/README.md) or [Gateway Only](../gateway-only/README.md)
- ✅ **Ollama running** locally (`ollama serve`) — or Azure OpenAI configured in `.env`
- ✅ **Sample documents** — Located in `docs/sampleDocs/` (included with repo)

## The 5 Scenarios

| # | Scenario | What You'll Learn | Status |
|---|----------|-------------------|--------|
| 1 | [Document Processing Pipeline](#1-document-processing-pipeline) | Scheduled jobs, file system tools, skill composition | ✅ Ready |
| 2 | [Event-Driven Conversation Kickoff](#2-event-driven-conversation-kickoff) | Job events, async handoff, pre-populated context | 🚧 Coming Soon |
| 3 | [Alert Orchestration](#3-alert-orchestration) | Multi-channel notifications, skill composition, secrets | 🚧 Coming Soon |
| 4 | [Multi-Agent Triage Pipeline](#4-multi-agent-triage-pipeline) | Agent routing, cost-aware models, job orchestration | 🚧 Coming Soon |
| 5 | [Compliance & Audit Logging](#5-compliance--audit-logging) | Structured logging, tool tracing, audit reports | 🚧 Coming Soon |

---

## 1. Document Processing Pipeline

**Overview:**  
A scheduled job monitors a folder, processes documents using a custom skill, generates AI summaries, and stores results in a searchable index.

**Recommended for:** Learning scheduler patterns, file system integration, skill composition, streaming notifications.

**Quick Start:**
```bash
# 1. Set up the demo job
curl -X POST http://localhost:5010/api/demos/doc-pipeline/setup

# 2. Check job status
curl http://localhost:5010/api/demos/doc-pipeline/status

# 3. View processed documents
curl http://localhost:5010/api/demos/doc-pipeline/results
```

[📖 Full Document Processing Guide →](01-document-pipeline/README.md)

---

## 2. Event-Driven Conversation Kickoff

**Overview:**  
When document processing completes, automatically create a new conversation, feed it the summary and extracted insights, and let the agent generate actionable next steps.

**Recommended for:** Job completion events, conversation context pre-population, event-driven patterns.

Status: 🚧 *Coming soon — Dallas working on Scenario 1 first, foundation for this scenario.*

[📖 Event-Driven Kickoff Placeholder →](02-event-driven-kickoff/README.md)

---

## 3. Alert Orchestration

**Overview:**  
Notification skill sends alerts via Email, Slack, or Teams based on agent reasoning and compliance flags. Shows how to abstract multiple external service integrations.

**Recommended for:** Multi-channel integration, provider abstraction, error handling, secrets management.

Status: 🚧 *Coming soon — builds on Scenarios 1–2.*

[📖 Alert Orchestration Placeholder →](03-alert-orchestration/README.md)

---

## 4. Multi-Agent Triage Pipeline

**Overview:**  
First agent (fast, local) does initial triage and routes documents. Category A (auto-resolved) returns immediately. Category B (human review) queues for approval. Category C (complex, high-value) escalates to Azure OpenAI for deep analysis. Shows model abstraction and cost-aware routing.

**Recommended for:** Agent routing patterns, cost optimization, model abstraction, job dependencies.

Status: 🚧 *Coming soon — builds on Scenarios 1–3.*

[📖 Multi-Agent Triage Placeholder →](04-multi-agent-triage/README.md)

---

## 5. Compliance & Audit Logging

**Overview:**  
Every agent action (tool call, decision, model swap, notification) is logged with reasoning. Background job periodically generates audit summaries and compliance reports. Perfect for regulated industries.

**Recommended for:** Structured logging, audit trails, compliance narratives, report generation.

Status: 🚧 *Coming soon — completes the advanced demo suite.*

[📖 Compliance & Audit Logging Placeholder →](05-compliance-audit/README.md)

---

## Recommended Build Order

Follow this sequence to build upon each scenario's learnings:

```
1. Document Processing Pipeline
   ↓ (foundation: scheduler + files + skills)
2. Event-Driven Kickoff
   ↓ (adds: job events + context)
3. Alert Orchestration
   ↓ (adds: external integrations)
4. Multi-Agent Triage
   ↓ (adds: agent routing + cost logic)
5. Compliance & Audit
   (adds: observability + audit trails)
```

---

## Common Patterns Across Scenarios

### Scheduler-First Architecture
All scenarios use the OpenClaw .NET scheduler as a first-class citizen. Jobs are durable, observable, and can be chained.

### Skill Composition
Each scenario composes 2+ skills to solve business problems (e.g., "markdown converter" + "summarizer" = "document processor").

### IAgentProvider Abstraction
Scenarios 4–5 show how to swap models (Ollama → Azure OpenAI) without changing agent logic. All providers implement `IAgentProvider`, and you can manage them through the Model Providers page or API.

### Event-Driven Handoff
Scenarios 2–3 demonstrate async patterns where one job's output triggers another's input.

### Observability Built-In
All scenarios log structured data that feeds into Scenario 5's audit narrative.

---

## Troubleshooting

**Gateway not responding?**
```bash
curl http://localhost:5010/health
```
If health check fails, restart the Gateway: `dotnet run --project src/OpenClawNet.Gateway`

**Ollama latency issues?**
Pre-warm the model: `ollama pull gemma4:e2b && ollama show gemma4:e2b` (run once before starting demos)

**Sample documents missing?**
Verify `docs/sampleDocs/` exists with these files:
- `invoice.pdf` — multi-page financial document
- `report.docx` — technical report with figures
- `policy.pdf` — compliance document  
- `contract.txt` — legal text
- `memo.md` — unstructured notes

---

## Architecture Overview

```
Scheduler (IJobScheduler)
    ↓
    └─→ Job #1: Document Processing
            ├─ File System Tool (reads docs)
            ├─ Markdown Conversion Skill
            ├─ Summarization Skill
            └─ Vector Embeddings Store
                ↓
                └─→ Job Completion Event
                        ↓
                        └─→ Job #2: Conversation Kickoff
                                ├─ Chat API (pre-populated)
                                ├─ Triage Agent
                                └─ Notification Skill
```

---

## Next Steps

- **New to OpenClaw .NET?** Start with [Tutorials](../README.md) first
- **Want the foundation details?** Review [Architecture](../../architecture/components.md)
- **Ready to code?** Clone the repo and run Scenario 1

Happy building! 🚀
