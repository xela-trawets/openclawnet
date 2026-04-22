# 🔔 Scenario 3: Alert Orchestration

## Overview

A **notification skill** sends alerts via Email, Slack, or Teams based on agent reasoning and compliance flags. This scenario teaches you how to abstract multiple external service integrations and compose skills for real-world notification workflows.

**Status:** 🚧 Coming Soon  
**Time to complete:** 20–30 minutes (when ready)  
**Technologies:** Skill Composition, HTTP Integration, Email/Slack/Teams APIs, Secrets Management, Error Handling & Retries  
**Prerequisites:** Scenario 1 & 2 completed

---

## What You'll Learn

- ✅ Compose a multi-channel notification skill
- ✅ Abstract provider implementations (Email → SMTP, Slack → HTTP, Teams → webhook)
- ✅ Manage secrets securely (API keys, webhook URLs)
- ✅ Implement retry logic for external service calls
- ✅ Handle failures gracefully (fallback channels)
- ✅ Log notification sends for audit trails

---

## Coming Soon

This scenario extends Scenario 2 and demonstrates:

1. **Skill abstraction** — Single `INotificationSkill` interface, multiple providers
2. **Configuration-driven routing** — Send to Email OR Slack OR Teams (or all three)
3. **Secure secrets** — Use .env or Azure Key Vault for API credentials
4. **Retry + fallback** — If Slack fails, fall back to email
5. **Structured logging** — Every notification logged with timestamp, recipient, status

**Example output:**
```
Document "invoice.pdf" processed:
  → Email: ✅ sent to accounting@company.com
  → Slack: ⏳ retry (connection timeout) → ✅ sent after 2s
  → Audit: Logged in job run metadata
```

---

## Why This Matters

- **Production requirement:** Almost every workflow needs notifications
- **Complexity:** Managing multiple external APIs requires abstraction and error handling
- **Learning:** Shows how to build extensible, provider-agnostic integrations

---

## Foundation Needed

When this scenario is ready:

1. `NotificationSkill` interface in `Skills` namespace
2. Implementations: `EmailNotificationProvider`, `SlackNotificationProvider`, `TeamsNotificationProvider`
3. Dependency injection setup for picking providers
4. Examples of calling from within a job or agent

**Expected interface:**
```csharp
public interface INotificationSkill : ISkill
{
  Task SendAsync(NotificationRequest request);
}

public class NotificationRequest
{
  public string Channel { get; set; } // "email", "slack", "teams"
  public string To { get; set; }
  public string Subject { get; set; }
  public string Body { get; set; }
  public Dictionary<string, string> Metadata { get; set; }
}
```

---

## Check Back Soon

This scenario is in the roadmap and will be available after Scenarios 1 & 2 are completed.

**In the meantime:**
- Review [Skills Documentation](../../skills/README.md)
- Explore [Dependency Injection patterns](../../setup/dependency-injection.md)
- Study HTTP client patterns in .NET (HttpClientFactory)

---

## Next Scenario

Once Scenario 3 is ready: **Scenario 4: Multi-Agent Triage Pipeline** — Route documents to different agents based on complexity and cost constraints.
