# 📋 Scenario 5: Compliance & Audit Logging

## Overview

Every agent action—tool calls, decisions, model swaps, notifications—is logged with structured data and reasoning. A background job periodically generates compliance narratives and audit reports. Perfect for regulated industries (finance, healthcare, legal) where audit trails are mandatory.

**Status:** 🚧 Coming Soon  
**Time to complete:** 25–35 minutes (when ready)  
**Technologies:** Structured Logging, Tool Call Tracing, Audit Storage, Report Generation Skills, Compliance Rules  
**Prerequisites:** Scenarios 1–4 completed

---

## What You'll Learn

- ✅ Implement structured logging for all agent actions
- ✅ Trace tool calls with input/output/reasoning
- ✅ Capture decision rationale (why agent chose action A vs B)
- ✅ Log model swaps and cost decisions
- ✅ Store conversation history as audit evidence
- ✅ Generate compliance narratives programmatically
- ✅ Query audit logs by compliance rule or date range

---

## Coming Soon

This scenario capstones the real-world demo suite and demonstrates:

1. **Audit event schema** — Structured data for every agent action
2. **Decision logging** — "Why did agent send notification?" → stored with reasoning
3. **Tool tracing** — Every tool call logged: input params, output, latency, cost
4. **Model tracking** — Which model, which prompt, token usage
5. **Periodic reports** — Background job generates "Weekly Compliance Summary"
6. **Searchable archive** — Query "show all financial decisions made by agent in March"

**Example audit entry:**
```json
{
  "timestamp": "2026-04-20T14:32:15Z",
  "eventId": "audit-evt-00123",
  "conversationId": "conv-invoice-001",
  "agentAction": "SendNotification",
  "toolName": "NotificationSkill",
  "toolInputs": {
    "channel": "email",
    "to": "finance@company.com",
    "subject": "Invoice overdue - action required",
    "body": "..."
  },
  "toolOutputs": {
    "status": "sent",
    "messageId": "msg-456",
    "timestamp": "2026-04-20T14:32:18Z"
  },
  "agentReasoning": "Invoice is 30 days past due. Automatic escalation per policy rule: FinancePolicy.Overdue30Days.",
  "model": "ollama:gemma4:e2b",
  "tokensUsed": 245,
  "estimatedCost": "$0.00",
  "complianceFlags": ["FINANCE_CRITICAL", "AUTO_ACTION"]
}
```

---

## Audit Report Example

**Daily Compliance Summary (2026-04-20):**

```
┌─────────────────────────────────────────────┐
│ Compliance Report: 2026-04-20               │
├─────────────────────────────────────────────┤
│ Events logged: 847                          │
│ Agent actions: 231                          │
│ Tool calls: 612                             │
│ Compliance flags triggered: 34              │
│                                             │
│ Top actions by frequency:                   │
│  1. SendNotification (142)                  │
│  2. ArchiveDocument (89)                    │
│  3. QueryIndex (238)                        │
│                                             │
│ Critical alerts (flagged for review):       │
│  • OverdueInvoice.SendNotification (8)      │
│  • LargeTransaction.EscalateToHuman (3)     │
│  • ComplianceViolation.BlockAction (1)      │
│                                             │
│ Model usage:                                │
│  • Ollama (gemma4): 245k tokens ($0.00)     │
│  • Azure OpenAI: 18k tokens ($0.65)         │
│                                             │
│ Evidence vault:                             │
│  • Conversations archived: 412              │
│  • Search index entries: 1,247              │
│  • Audit logs: 847 events                   │
│                                             │
│ ✅ All compliance rules satisfied           │
└─────────────────────────────────────────────┘
```

---

## Compliance Rules (Example)

```csharp
public static class ComplianceRules
{
  // Rule: Financial decisions over $10k require model swap to expensive
  public static bool RequiresExpensiveModel(decimal amount)
    => amount > 10_000;
  
  // Rule: Flag any agent escalation to human for audit
  public static bool IsComplianceCritical(string action)
    => action.Contains("Escalate") || action.Contains("Block");
  
  // Rule: Log all notification sends
  public static bool AuditableAction(string toolName)
    => toolName == "NotificationSkill";
}
```

---

## Why This Matters

- **Regulated industries:** Finance, healthcare, legal all require audit trails
- **Risk management:** "Why did the agent do that?" must be answerable
- **Compliance:** SOC 2, HIPAA, GDPR all demand logging and traceability
- **Debugging:** When something goes wrong, full audit trail enables root cause analysis

---

## Architecture: Audit Pipeline

```
Agent executes action
    ↓
Listener captures event
    ├─ tool name, inputs, outputs
    ├─ reasoning, decision rationale
    ├─ model used, tokens, cost
    └─ compliance flags
    ↓
Structured log written to database
    ├─ Indexed for fast search
    └─ Versioned (immutable append)
    ↓
Background job (nightly)
    ├─ Aggregate events by rule
    ├─ Generate narrative summary
    └─ Store in reports table
    ↓
Query API: "/api/audit/reports/{date}"
    └─ Returns: PDF, JSON, or markdown narrative
```

---

## Foundation Needed

When this scenario is ready:

1. `IAuditLogger` service interface
2. `AuditEvent` entity for structured logging
3. Job for periodic report generation
4. Query API: `/api/audit/events`, `/api/audit/reports`
5. Compliance rule engine
6. Report generation skill (markdown/PDF output)

**Expected interface:**
```csharp
public interface IAuditLogger
{
  Task LogToolCallAsync(AuditEvent auditEvent);
  Task<List<AuditEvent>> QueryAsync(AuditFilter filter);
}

public class AuditEvent
{
  public string Id { get; set; } // immutable
  public DateTime Timestamp { get; set; }
  public string ConversationId { get; set; }
  public string ToolName { get; set; }
  public Dictionary<string, object> Inputs { get; set; }
  public Dictionary<string, object> Outputs { get; set; }
  public string AgentReasoning { get; set; }
  public List<string> ComplianceFlags { get; set; }
}
```

---

## Check Back Soon

This capstone scenario is in the roadmap and will launch after Scenarios 1–4 are complete.

**In the meantime:**
- Explore [Structured Logging](../../architecture/logging.md)
- Review [Conversation History Storage](../../architecture/conversations.md)
- Study [Report generation patterns](../../skills/report-generation.md)
- Read about [Compliance frameworks](../../guides/compliance.md)

---

## Completing the Real-World Suite

After Scenario 5, you'll have built:

```
✅ Scenario 1: Scheduled document processing
✅ Scenario 2: Event-driven workflow automation
✅ Scenario 3: Multi-channel integrations
✅ Scenario 4: Intelligent routing & cost optimization
✅ Scenario 5: Compliance & auditability

= Production-ready agent system
```

**Next steps:**
- Deploy to Azure Container Instances or Kubernetes
- Integrate with your data systems
- Add custom skills for your domain
- Hire agent platform experts to extend further

Happy auditing! 📋
