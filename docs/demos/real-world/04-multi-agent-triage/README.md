# 🎯 Scenario 4: Multi-Agent Triage Pipeline

## Overview

A **two-stage triage pipeline** routes documents intelligently. First agent (fast, local Ollama) does initial analysis and routes to Category A (auto-resolved), Category B (human review queue), or Category C (complex, escalates to Azure OpenAI for deep analysis). This scenario teaches cost-aware agent orchestration and model abstraction.

**Status:** 🚧 Coming Soon  
**Time to complete:** 25–35 minutes (when ready)  
**Technologies:** Agent Routing, IAgentProvider Abstraction, Job Dependencies, Cost Optimization, Conditional Scheduling  
**Prerequisites:** Scenario 1, 2, & 3 completed, Azure OpenAI optional (falls back to Ollama)

---

## What You'll Learn

- ✅ Route agent invocations based on content analysis
- ✅ Swap models dynamically (Ollama → Azure OpenAI) via the `IAgentProvider` abstraction
- ✅ Implement cost-aware decision-making (when to use expensive model)
- ✅ Chain dependent jobs (Job A completes → conditionally trigger Job B)
- ✅ Classify documents into categories with confidence scores
- ✅ Queue documents for human review

---

## Coming Soon

This scenario builds on Scenarios 1–3 and demonstrates:

1. **Triage agent (local)** — Fast, lightweight categorization using Ollama
2. **Cost-aware routing** — Use local model first, escalate only when needed
3. **Category handling:**
   - **Category A** (auto-resolved) → Return immediately with agent-generated action
   - **Category B** (human review) → Queue in database, notify manager
   - **Category C** (complex) → Submit to second job using Azure OpenAI
4. **Job chaining** — Schedule Category C processing only if needed
5. **Cost metrics** — Log which model was used, tokens consumed, cost per document

**Example flow:**
```
Document: "Urgent: Server down, all services affected"
    ↓
Triage Agent (Ollama, fast):
  "This is critical and urgent. Category: C (high complexity)"
  Confidence: 95%
    ↓
Route to expensive model → Schedule Job for Azure OpenAI
    ↓
Deep Agent (Azure OpenAI):
  "Immediate actions: (1) Declare incident, (2) Page on-call team,
   (3) Activate runbook-4-server-outage, (4) Notify customers."
    ↓
Result: Detailed remediation steps + escalation chain
```

---

## Architecture: Two-Agent Pipeline

```
Input Document
    ↓
    └─→ Job 1: Triage (IJobScheduler)
            Executor: Triage Agent (Ollama model)
            Output: Category + Confidence
            ↓
        Decision Tree:
        ├─→ Category A? → Auto-resolve (return)
        ├─→ Category B? → Queue for human (store in DB)
        └─→ Category C? → Escalate (schedule Job 2)
                ↓
                └─→ Job 2: Deep Analysis (conditional)
                        Executor: Deep Agent (Azure OpenAI model)
                        Input: Document + triage summary
                        Output: Detailed recommendations

```

---

## Why This Matters

- **Cost optimization:** Local models are free; expensive cloud models are used strategically
- **Real-world:** Enterprise document processing always routes by complexity
- **IAgentProvider abstraction:** Shows how to build model-agnostic agents
- **Job dependencies:** Advanced scheduler feature that enables conditional workflows

---

## Key Concepts

### IAgentProvider Abstraction
Two implementations (among 6 total):
```csharp
// Local (free, fast)
var ollama = container.GetRequiredService<OllamaAgentProvider>();
// Cost: ~0, latency: 1-2s

// Cloud (expensive, powerful)
var azure = container.GetRequiredService<AzureOpenAIAgentProvider>();
// Cost: $0.02 per 1k tokens, latency: 200-500ms
```

### Cost-Aware Routing
```csharp
if (confidence < 0.7)
{
  // Use expensive model only if triage is uncertain
  var deepAgent = new Agent(azureOpenAIClient);
}
else
{
  // Use free model if confident
  var autoResolution = await GenerateAutoResolution(triageResult);
}
```

### Conditional Job Scheduling
```csharp
// Only schedule expensive job if needed
if (needsDeepAnalysis)
{
  await scheduler.ScheduleAsync(new JobDefinition
  {
    Type = "DeepAnalysisJob",
    Inputs = new { documentId, triageSummary },
    ExecuteAfterJobId = triageJobRunId // dependency
  });
}
```

---

## Foundation Needed

When this scenario is ready:

1. Multi-model support in `IAgentOrchestrator`
2. `IAgentProvider` abstraction (already in codebase — 6 providers implemented)
3. Job dependencies in Scheduler (`ExecuteAfterJobId` field)
4. Conditional job scheduling logic
5. Cost tracking in job run metadata

---

## Check Back Soon

This scenario is in development and will launch after Scenarios 1–3 are complete.

**In the meantime:**
- Review [IAgentProvider design](../../architecture/provider-model.md)
- Explore [Job Scheduling](../../architecture/scheduler.md)
- Study [Agent abstraction patterns](../../architecture/agents.md)
- Read about [Cost optimization strategies](../../guides/cost-optimization.md)

---

## Next Scenario

Once Scenario 4 is ready: **Scenario 5: Compliance & Audit Logging** — Create audit trails for all agent decisions and generate compliance reports.
