# Tool Approval System Design

**Author:** Ripley (Lead / Architect)  
**Date:** 2026-04-19  
**Status:** Architecture Proposal — Awaiting Bruno's Decisions  
**Branch:** `analysis/ripley-tool-approval`

---

## Table of Contents

1. [Problem Statement](#1-problem-statement)
2. [Current State](#2-current-state)
3. [Design Options](#3-design-options)
   - [Axis A: Approval Scope Granularity](#axis-a-approval-scope-granularity)
   - [Axis B: Where Approval State Lives](#axis-b-where-approval-state-lives)
   - [Axis C: UI Flow](#axis-c-ui-flow)
   - [Axis D: Unattended Execution](#axis-d-unattended-execution-cron-jobs)
4. [Pros/Cons Matrix](#4-proscons-matrix)
5. [Recommended Combination](#5-recommended-combination)
6. [Open Questions for Bruno](#6-open-questions-for-bruno)
7. [Out of Scope (v1)](#7-out-of-scope-v1)
8. [Implementation Sketch](#8-implementation-sketch-after-brunos-decisions)

---

## 1. Problem Statement

OpenClawNet agents can invoke powerful tools — shell commands, file system writes, browser automation, scheduler modifications — that carry real-world side effects. Currently, tools marked `RequiresApproval = true` emit a `ToolApprovalRequest` event but execution **proceeds immediately without waiting for user confirmation**. This creates a security and trust gap: users have no way to review potentially dangerous operations before they execute, undermining the human-in-the-loop safety principle essential for production agent deployments. The tool approval feature bridges this gap by introducing explicit user consent checkpoints, configurable at both the tool and agent profile level.

---

## 2. Current State

### What Exists (Partially Implemented)

| Component | File:Line | What's There | What's Missing |
|-----------|-----------|--------------|----------------|
| **Stream Event Type** | `src/OpenClawNet.Agent/AgentResponse.cs:26` | `AgentStreamEventType.ToolApprovalRequest` enum value | Event is emitted but not blocking |
| **Tool Metadata Flag** | `src/OpenClawNet.Tools.Abstractions/ToolMetadata.cs:10` | `RequiresApproval { get; init; }` property | Used for emission only, not enforcement |
| **Approval Policy Interface** | `src/OpenClawNet.Tools.Abstractions/ToolApprovalPolicy.cs:3-7` | `IToolApprovalPolicy` with `RequiresApprovalAsync()` + `IsApprovedAsync()` | Only `AlwaysApprovePolicy` implemented |
| **ToolExecutor Integration** | `src/OpenClawNet.Tools.Core/ToolExecutor.cs:27-32` | Calls `_approvalPolicy.RequiresApprovalAsync()` and `IsApprovedAsync()` | Policy never blocks (always returns approved) |
| **Runtime Event Emission** | `src/OpenClawNet.Agent/DefaultAgentRuntime.cs:376-384` | Emits `ToolApprovalRequest` event when `toolMeta?.RequiresApproval is true` | Doesn't await approval; immediately proceeds to `ToolCallStart` |
| **Gateway Mapping** | `src/OpenClawNet.Gateway/Endpoints/ChatStreamEndpoints.cs:248` | Maps `ToolApprovalRequest` → `"tool_approval"` NDJSON event | No endpoint to receive approval response |
| **Web UI Handler** | `src/OpenClawNet.Web/Components/Pages/Chat.razor:412-415` | Sets `_pendingApprovalTool` on event receipt | No UI to approve/deny; immediately cleared on `tool_start` |
| **AgentProfile Model** | `src/OpenClawNet.Models.Abstractions/AgentProfile.cs` | 12 properties (Name, Provider, Model, Instructions, etc.) | No `ApprovalPolicy` or `AutoApproveSensitiveTools` field |
| **ScheduledJob Model** | `src/OpenClawNet.Storage/Entities/ScheduledJob.cs` | `AgentProfileName`, `TriggerType`, `CronExpression` | No `UnattendedApprovalPolicy` field |

### Tools with `RequiresApproval = true`

| Tool | File | Reason |
|------|------|--------|
| `shell` | `src/OpenClawNet.Tools.Shell/ShellTool.cs:37` | Arbitrary command execution |
| `file_system` | `src/OpenClawNet.Tools.FileSystem/FileSystemTool.cs:53` | Read/write/list files |

### Tools with `RequiresApproval = false`

| Tool | File | Reason |
|------|------|--------|
| `web_fetch` | `src/OpenClawNet.Tools.Web/WebTool.cs:41` | Read-only HTTP GET/POST (blocks local IPs) |
| `browser` | `src/OpenClawNet.Tools.Browser/BrowserTool.cs:39` | Sandboxed browser automation service |
| `schedule` | `src/OpenClawNet.Tools.Scheduler/SchedulerTool.cs:46` | Creates jobs (debatable — see Open Questions) |

### Wire Protocol Gap

The NDJSON stream already carries `tool_approval` events:

```json
{"type":"tool_approval","toolName":"shell","toolDescription":"Execute safe shell commands...","sessionId":"..."}
```

But there's no:
1. Endpoint to POST approval/denial back
2. Mechanism to pause the agent's tool loop while awaiting approval
3. Session state tracking what's pending approval

---

## 3. Design Options

### Axis A: Approval Scope Granularity

**Question:** When the user approves a tool, what scope does that approval cover?

#### Option A1: Per-Tool-Invocation (Every Call Asks)

Every time the agent wants to call `shell`, user must approve. 10 shell calls = 10 approval prompts.

#### Option A2: Per-Tool-Type (Per-Session)

Approve "shell" once for this chat session; all subsequent shell calls auto-approve until session ends.

#### Option A3: Per-Tool-With-Args-Fingerprint

Approve `shell("ls -la")` separately from `shell("rm -rf /")`. Fingerprint = hash of (tool + args).

#### Option A4: Tiered Auto-Approval

Classify tools into tiers:
- **Safe (Tier 0):** Auto-approve always (web_fetch, browser)
- **Medium (Tier 1):** Prompt once per session (schedule)
- **Dangerous (Tier 2):** Prompt every invocation (shell, file_system)

---

### Axis B: Where Approval State Lives

**Question:** Where do we configure whether tools require approval?

#### Option B1: Per-AgentProfile Only

Add `ApprovalPolicy: enum { Manual, AutoApprove }` to `AgentProfile`. If `Manual`, all `RequiresApproval=true` tools prompt. If `AutoApprove`, none do.

```csharp
// AgentProfile.cs
public ApprovalMode ApprovalMode { get; set; } = ApprovalMode.Manual;
```

#### Option B2: Per-Tool Only

Each tool's `ToolMetadata.RequiresApproval` is the source of truth. No profile-level override. All profiles behave the same.

#### Option B3: Per-AgentProfile × Per-Tool Matrix

Rich configuration: for each profile, specify per-tool approval settings.

```csharp
// AgentProfile.cs
public Dictionary<string, ApprovalMode>? ToolApprovalOverrides { get; set; }
```

Example: Profile "production-safe" sets `shell=AlwaysPrompt`, `file_system=Deny`, `web_fetch=AutoApprove`.

#### Option B4: Global Default + Per-Profile Override

System-wide setting (e.g., env var or config file) defines default. Profiles can override.

```csharp
// appsettings.json
"ToolApproval": {
  "DefaultMode": "Manual"
}

// AgentProfile.cs
public ApprovalMode? ApprovalModeOverride { get; set; }  // null = use global
```

---

### Axis C: UI Flow

**Question:** How does the user interact with approval requests in the chat UI?

#### Option C1: Modal Dialog (Blocking)

Full-screen modal appears. Chat is blocked. User must click Approve/Deny. Agent pauses.

```
┌─────────────────────────────────────────────────┐
│  🛡️ Tool Approval Required                      │
│                                                 │
│  The agent wants to execute:                    │
│                                                 │
│    shell("ls -la /home/user/documents")         │
│                                                 │
│  ┌─────────┐  ┌─────────┐                       │
│  │ Approve │  │  Deny   │                       │
│  └─────────┘  └─────────┘                       │
└─────────────────────────────────────────────────┘
```

#### Option C2: Inline Approval Card (In Chat Thread)

Approval request appears as a special message bubble in the chat. Agent pauses. User clicks inline buttons.

```
┌────────────────────────────────────────────────────────────┐
│ 🤖 Assistant                                               │
│ I'll check your documents folder. Let me run a command...  │
├────────────────────────────────────────────────────────────┤
│ 🛡️ Awaiting Approval                                       │
│                                                            │
│ Tool: shell                                                │
│ Command: ls -la /home/user/documents                       │
│                                                            │
│ [✓ Approve]  [✗ Deny]  [📋 View Full Args]                 │
└────────────────────────────────────────────────────────────┘
```

#### Option C3: Side Panel Notification

Chat continues scrolling. A non-blocking side panel or bottom drawer shows pending approvals. Agent pauses but chat input stays enabled.

#### Option C4: Toast with Timeout (Auto-Deny)

Toast notification appears. If user doesn't respond in N seconds (e.g., 30s), auto-deny and agent continues with fallback behavior.

---

### Axis D: Unattended Execution (Cron Jobs)

**Question:** What happens when a scheduled job runs with no user present to approve?

#### Option D1: Force Auto-Approve for Cron

All cron/scheduled jobs ignore `RequiresApproval` — tools execute unconditionally. Rationale: user consented when creating the job.

#### Option D2: Fail If Approval Required

If a cron job's agent profile has `ApprovalMode = Manual` and a tool needs approval, the job **fails immediately** with a clear error. User must either change profile or tool.

#### Option D3: Queue for Approval, Resume Later

Pending approvals are persisted. Next time a user opens the UI, they see "Job X paused for approval." Approve → job resumes. Deny → job fails.

#### Option D4: Per-Profile "Unattended Fallback Policy"

Each `AgentProfile` specifies what to do when running unattended:

```csharp
public enum UnattendedApprovalFallback
{
    AutoApprove,   // Approve all sensitive tools when no user present
    Deny,          // Deny all sensitive tools (job continues with errors)
    FailJob,       // Abort the entire job
    Queue          // Pause and wait for user (resume on next UI visit)
}

// AgentProfile.cs
public UnattendedApprovalFallback UnattendedFallback { get; set; } = UnattendedApprovalFallback.Deny;
```

---

## 4. Pros/Cons Matrix

### Axis A: Approval Scope Granularity

| Option | Pros | Cons | Complexity | Risk |
|--------|------|------|------------|------|
| **A1: Per-Invocation** | Maximum safety; user sees every action; simple to implement | Extremely annoying for multi-tool tasks (10+ prompts); kills agent autonomy | S | Low |
| **A2: Per-Tool-Type (Session)** | Good balance — approve once, then flow; intuitive UX | Could miss dangerous command variants; session-scoped means re-approve on refresh | S | Low |
| **A3: Args-Fingerprint** | Most granular; different commands get different approvals | Complex fingerprinting logic; cache invalidation headaches; UX confusion ("I approved shell before!") | L | Med |
| **A4: Tiered** | Matches real risk profiles; no prompts for safe tools; always prompt for dangerous | Tier classification is subjective; needs ongoing curation; more config surface | M | Med |

### Axis B: Where Approval State Lives

| Option | Pros | Cons | Complexity | Risk |
|--------|------|------|------------|------|
| **B1: Per-AgentProfile** | Simple UI (one toggle); clear per-agent semantics; matches JobEditor pattern | Coarse — all-or-nothing per profile; can't approve shell but deny file_system | S | Low |
| **B2: Per-Tool Only** | Zero profile changes; tools self-describe safety | No user control; can't relax for dev environments or tighten for production | S | Low |
| **B3: Profile × Tool Matrix** | Maximum flexibility; fine-grained control | Complex UI (matrix editor); more DB columns; migration needed; cognitive load | L | Med |
| **B4: Global + Override** | Sensible defaults out-of-box; profiles only override when needed | Two layers of config to reason about; default could be too permissive/strict | M | Low |

### Axis C: UI Flow

| Option | Pros | Cons | Complexity | Risk |
|--------|------|------|------------|------|
| **C1: Modal** | Impossible to miss; clear call-to-action; blocks mistakes | Disruptive; can't read chat while deciding; feels heavy for low-risk tools | S | Low |
| **C2: Inline Card** | Contextual (in chat thread); less disruptive; can scroll up to see context | Card could scroll off-screen if agent was verbose; needs careful Blazor state | M | Low |
| **C3: Side Panel** | Non-blocking; can queue multiple approvals; power-user friendly | Easy to ignore; split attention; mobile-unfriendly | M | Med |
| **C4: Toast + Timeout** | Least disruptive; auto-deny prevents hanging | User might miss it; auto-deny could abort important work; timeout tuning needed | M | High |

### Axis D: Unattended Execution (Cron Jobs)

| Option | Pros | Cons | Complexity | Risk |
|--------|------|------|------------|------|
| **D1: Force Auto-Approve** | Jobs always run; simplest; "you approved the job, you approved the tools" | Bypasses safety entirely for cron; shell injection in prompt = game over | S | High |
| **D2: Fail If Approval Required** | Clear failure mode; forces explicit profile config; safe by default | Jobs with sensitive tools always fail unless profile allows; could surprise users | S | Low |
| **D3: Queue for Later** | Jobs pause gracefully; no data loss; resume when user returns | Job might be stale by the time user approves; complex state persistence | L | Med |
| **D4: Per-Profile Fallback** | Maximum flexibility; user explicitly chooses behavior; audit-friendly | More config; user must understand 4 options; default matters a lot | M | Low |

---

## 5. Recommended Combination

**My pick for MVP:**

| Axis | Recommendation | Rationale |
|------|----------------|-----------|
| **A** | **A2: Per-Tool-Type (Session)** | Best UX balance. Approve `shell` once, agent can use it freely in that session. Avoids prompt fatigue while maintaining first-use consent. Easy to implement (session-scoped Set). |
| **B** | **B1: Per-AgentProfile** | Simplest to implement and explain. One toggle: "Require manual approval" (yes/no). Profiles like "production-safe" set yes; "dev-sandbox" set no. Extends existing profile model with 1 field. |
| **C** | **C2: Inline Approval Card** | Contextual and visible without being disruptive. Fits naturally in chat flow. User can scroll up for context. Already partially wired (Chat.razor handles `tool_approval` event). |
| **D** | **D2: Fail If Approval Required** | Safe by default. If profile says "manual approval" and it's a cron job with no user, fail fast with clear error. Forces users to consciously create "auto-approve" profiles for unattended work. |

**Why this combination:**

1. **Ships fast:** Each component is S-M complexity. Total effort: ~3-4 days.
2. **Safe by default:** Manual approval is the default; users opt-in to auto-approve.
3. **UX-friendly:** Session-scoped approval + inline cards = minimal friction after first approval.
4. **Debuggable:** Failures are explicit and logged; no silent auto-approvals.
5. **Extensible:** Can upgrade to B3 (per-tool matrix) or D4 (per-profile fallback) later without breaking changes.

---

## 6. Open Questions for Bruno

Please provide decisions on these before implementation begins:

### Question 1: Default Approval Mode for New Profiles

> When a user creates a new Agent Profile, should the default be:
> 
> **A)** `RequireApproval = true` (safe by default — user must approve sensitive tools)  
> **B)** `RequireApproval = false` (autonomous by default — tools execute freely)

**My recommendation:** A (safe by default).

---

### Question 2: Should `schedule` Tool Require Approval?

> The `schedule` tool creates/modifies scheduled jobs. Currently `RequiresApproval = false`. Should it be `true`?
> 
> **A)** Yes — creating jobs is a powerful action; user should confirm  
> **B)** No — job creation is relatively safe (jobs still need to execute)

**My recommendation:** A (yes) — an agent creating arbitrary jobs is a footgun.

---

### Question 3: Approval Timeout for Interactive Sessions

> If the user doesn't respond to an approval request in an interactive chat, what happens?
> 
> **A)** Wait indefinitely (agent pauses forever until approve/deny)  
> **B)** Auto-deny after N seconds (default 60s) and agent continues with error  
> **C)** Auto-deny after N seconds and retry once with a reminder

**My recommendation:** A (wait indefinitely for v1) — simpler, avoids lost work.

---

### Question 4: "Remember for This Session" vs "Remember for This Tool"

> When user approves a tool, should we show a checkbox "Remember for this session"?
> 
> **A)** Yes — gives user control over session-scope approval  
> **B)** No — always session-scoped (A2 option), simpler UX

**My recommendation:** B for MVP, add A in v2 if requested.

---

### Question 5: Browser Tool Sensitivity Upgrade?

> The `browser` tool can navigate to arbitrary URLs, fill forms, click buttons. Should it be upgraded to `RequiresApproval = true`?
> 
> **A)** Yes — browser automation is powerful; same tier as shell  
> **B)** No — it's sandboxed in a separate service; current classification is fine

**My recommendation:** A (yes) — form filling + clicking = can do real damage.

---

## 7. Out of Scope (v1)

The following are explicitly **not** part of the initial implementation:

| Feature | Why Deferred |
|---------|--------------|
| **Approval Audit Log** | Valuable for compliance but adds DB schema + UI complexity. Revisit after basic approval works. |
| **Multi-User RBAC** | OpenClawNet is currently single-user. Approval ownership/delegation requires user model first. |
| **Approval Delegation** | "Alice approved for Bob" — needs user identity + permissions system. |
| **Per-Tool-With-Args Fingerprinting** | Complexity outweighs value for MVP. Session-scoped per-tool is sufficient. |
| **Approval Timeouts** | Per Q3 above — wait indefinitely for v1. Timeouts add edge cases. |
| **Tool Allowlist/Denylist per Profile** | Would be nice but adds matrix UI. Use profile-level toggle for v1. |
| **Webhook-Triggered Approval** | (e.g., Slack approval flow) — cool but significant scope. |
| **Mobile-Optimized Approval UI** | Current Blazor UI is desktop-first. Mobile can use responsive cards. |
| **Approval Rate Limiting** | (e.g., "max 5 approvals per minute") — over-engineering for v1. |

---

## 8. Implementation Sketch (After Bruno's Decisions)

Below is a forward-looking outline of PRs — **no code will be written until Bruno confirms the approach**.

### PR 1: Domain Model + Storage (Foundation)

**Scope:** Add approval-related fields to `AgentProfile` and `AgentProfileEntity`. Migration.

**Files:**

| Path | Change |
|------|--------|
| `src/OpenClawNet.Models.Abstractions/AgentProfile.cs` | Add `RequireToolApproval: bool` (default `true`) |
| `src/OpenClawNet.Models.Abstractions/ApprovalMode.cs` | (New) Enum if needed for future expansion |
| `src/OpenClawNet.Storage/Entities/AgentProfileEntity.cs` | Add `RequireToolApproval` column |
| `src/OpenClawNet.Storage/AgentProfileStore.cs` | Map new field in CRUD |
| `src/OpenClawNet.Storage/SchemaMigrator.cs` | Add column to existing profiles (default `true`) |

**Effort:** 2-3 hours

---

### PR 2: Approval Policy Implementation (Backend)

**Scope:** Create a real `IToolApprovalPolicy` that tracks session state and blocks execution.

**Files:**

| Path | Change |
|------|--------|
| `src/OpenClawNet.Tools.Abstractions/IToolApprovalPolicy.cs` | Add `AwaitApprovalAsync()`, `GrantApprovalAsync()`, `DenyApprovalAsync()` |
| `src/OpenClawNet.Tools.Core/SessionToolApprovalPolicy.cs` | (New) In-memory session-scoped approval tracking |
| `src/OpenClawNet.Tools.Core/ToolExecutor.cs` | Await approval before execution |
| `src/OpenClawNet.Agent/DefaultAgentRuntime.cs` | Integrate approval pause into streaming tool loop |
| `src/OpenClawNet.Gateway/Services/ApprovalStateManager.cs` | (New) Tracks pending approvals per session |

**Effort:** 4-6 hours

---

### PR 3: Gateway API Endpoints

**Scope:** Add endpoints for UI to send approval/denial responses.

**Files:**

| Path | Change |
|------|--------|
| `src/OpenClawNet.Gateway/Endpoints/ApprovalEndpoints.cs` | (New) `POST /api/sessions/{sessionId}/tools/{toolName}/approve`, `POST .../deny` |
| `src/OpenClawNet.Gateway/Endpoints/ChatStreamEndpoints.cs` | Wire approval state manager into stream handler |
| `src/OpenClawNet.Gateway/Program.cs` | Register new endpoints |

**Effort:** 2-3 hours

---

### PR 4: Web UI Approval Card

**Scope:** Render inline approval card in Chat.razor with Approve/Deny buttons.

**Files:**

| Path | Change |
|------|--------|
| `src/OpenClawNet.Web/Components/Pages/Chat.razor` | Render approval card when `_pendingApprovalTool` is set; call approval API |
| `src/OpenClawNet.Web/Components/Shared/ToolApprovalCard.razor` | (New) Reusable approval card component |
| `src/OpenClawNet.Web/wwwroot/css/app.css` | Styling for approval card |

**Effort:** 3-4 hours

---

### PR 5: Profile Editor UI

**Scope:** Add "Require Tool Approval" toggle to AgentProfiles page.

**Files:**

| Path | Change |
|------|--------|
| `src/OpenClawNet.Web/Components/Pages/AgentProfiles.razor` | Add toggle in profile editor form |
| `src/OpenClawNet.Gateway/Endpoints/AgentProfileEndpoints.cs` | Ensure field is returned/accepted in API |

**Effort:** 1-2 hours

---

### PR 6: Unattended Execution Handling

**Scope:** Make `JobExecutor` fail fast when profile requires approval.

**Files:**

| Path | Change |
|------|--------|
| `src/OpenClawNet.Gateway/Services/JobExecutor.cs` | Check `profile.RequireToolApproval` + `job.TriggerType`; if cron + requires approval → fail |
| `src/OpenClawNet.Storage/Entities/JobRun.cs` | Add `ApprovalDenied` failure reason |
| `tests/OpenClawNet.Tests.Integration/JobExecutorTests.cs` | Test unattended approval failure |

**Effort:** 2-3 hours

---

### Total Estimated Effort

| PR | Hours |
|----|-------|
| PR 1: Domain Model | 2-3 |
| PR 2: Approval Policy | 4-6 |
| PR 3: Gateway API | 2-3 |
| PR 4: Web UI Card | 3-4 |
| PR 5: Profile Editor | 1-2 |
| PR 6: Unattended Handling | 2-3 |
| **Total** | **14-21 hours** (~2-3 days focused work) |

---

## Summary

This document presents a comprehensive design for tool approval in OpenClawNet. The recommended MVP uses:

- **Per-tool-type session-scoped approval** (A2) — approve once, auto-approve thereafter
- **Per-AgentProfile toggle** (B1) — simple "require approval" boolean
- **Inline approval card** (C2) — contextual, non-modal UI
- **Fail-fast for unattended jobs** (D2) — safe by default

Five open questions need Bruno's input before implementation can begin. The estimated effort is 2-3 days across 6 focused PRs, building incrementally on existing partial infrastructure (`ToolApprovalRequest` events, `IToolApprovalPolicy` interface, `RequiresApproval` metadata).

**Next step:** Bruno answers questions 1-5 → Team can begin implementation.

---

*Document generated by Ripley (Lead / Architect) — OpenClawNet Squad*
