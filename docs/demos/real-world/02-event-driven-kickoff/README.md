# 🔗 Scenario 2: Event-Driven Conversation Kickoff

## Overview

When the document processing job (Scenario 1) completes, automatically create a new conversation and pre-populate it with the document summary and extracted insights. The agent then generates actionable next steps based on the document.

**Status:** 🚧 Coming Soon  
**Time to complete:** 20–30 minutes (when ready)  
**Technologies:** Job Completion Events, Chat API, Conversation Context, Async Handoff  
**Prerequisites:** Scenario 1 completed, Documents processed successfully

---

## What You'll Learn

- ✅ Subscribe to job completion events
- ✅ Create conversations programmatically with pre-populated context
- ✅ Trigger agent reasoning from job outputs
- ✅ Implement event-driven async workflows
- ✅ Chain jobs together using the Scheduler

---

## Coming Soon

This scenario builds on Scenario 1 and demonstrates how to:

1. **Listen for job completion** — Hook into job run completion events
2. **Extract context** — Get the processed document summary from the job run
3. **Create a conversation** — POST to `/api/chat/conversations` with initial context
4. **Seed the agent** — Pre-populate the conversation history with the document insight
5. **Let the agent act** — Agent reads context and generates next steps

**Example flow:**
```
Scenario 1 completes: Document processing done
    ↓
Event fires: "DocumentProcessed"
    ↓
Extract: Summary + key topics
    ↓
Create conversation with context
    ↓
Agent generates: "This invoice is overdue. Recommend: Send payment reminder."
    ↓
(Scenario 3 extends this: send notification)
```

---

## Why This Matters

- **Real-world:** Most production systems are event-driven. Jobs complete → triggers the next step.
- **Learning:** Shows how to compose Scenarios into a workflow.
- **Platform strength:** OpenClaw .NET has first-class event support via job completion hooks.

---

## Foundation Needed

This scenario depends on Scenario 1 working correctly. When Scenario 2 is ready:

1. You'll have access to the `IJobCompletionListener` interface
2. Documentation will include curl examples for subscribing to events
3. Detailed code walkthrough of event wiring in Dependency Injection

---

## Check Back Soon

Dallas and the team are actively building this scenario. Follow the [Real-World Demos README](../README.md) for status updates.

**In the meantime:**
- Deep-dive into [Scenario 1: Document Processing](../01-document-pipeline/README.md)
- Explore [Scheduler documentation](../../architecture/scheduler.md)
- Read about [Conversation Context](../../architecture/conversations.md)

---

## Next Scenario

Once Scenario 2 is ready: **Scenario 3: Alert Orchestration** — Add multi-channel notifications (Email, Slack, Teams) based on agent reasoning.
