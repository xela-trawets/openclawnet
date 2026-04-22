# Jobs + MAF Implementation Delta
**Author:** Ripley (Lead / Architect)  
**Date:** 2026-04-28  
**Purpose:** Reconcile the rollout plan (PRs #1-#12) with actual codebase state

---

## Summary

**Audit Outcome:** PRs #1-#4 (IAgentProvider + 5 providers + RuntimeAgentProvider) are **COMPLETE ✅**. PR #5 (MAF Runtime Cutover) is **PARTIAL 🟡** — `IAgentProvider` creates `IChatClient`, not full `AIAgent`; manual tool loop still in place. PR #6+ are **NOT STARTED 🔴**.

**First real work:** PR #5 (MAF Runtime Cutover) — needs completion, OR skip directly to PR #6 (Jobs Domain Model) if we defer full MAF cutover.

**Recommendation:** Skip PR #5 for now (defer to post-Jobs). Start with **PR #6 (Jobs Domain Model + Migrations)** — clean, isolated, and unblocks Jobs UI work.

---

## PR-by-PR Status

### PR #1: IAgentProvider Interface + Ollama Provider
**Status:** ✅ **COMPLETE**

**Evidence:**
- `src/OpenClawNet.Models.Abstractions/IAgentProvider.cs` exists
- `src/OpenClawNet.Models.Ollama/OllamaAgentProvider.cs` exists
- `tests/OpenClawNet.UnitTests/Models/OllamaAgentProviderTests.cs` exists

**Actual Implementation:**
```csharp
public interface IAgentProvider
{
    string ProviderName { get; }
    IChatClient CreateChatClient(AgentProfile profile);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
```

**Variance from Plan:**
- Returns `IChatClient` (not `AIAgent`) — Phase 1 pragmatic choice to avoid breaking existing runtime
- Interface in `OpenClawNet.Models.Abstractions` (not `OpenClawNet.Runtime` — no such project exists)
- Tests in `OpenClawNet.UnitTests/Models/` (not separate `Runtime.Tests` project)

**Conclusion:** Complete, with architectural variance that's acceptable. No rework needed.

---

### PR #2: Azure OpenAI + Foundry Providers
**Status:** ✅ **COMPLETE**

**Evidence:**
- `src/OpenClawNet.Models.AzureOpenAI/AzureOpenAIAgentProvider.cs` exists
- `src/OpenClawNet.Models.Foundry/FoundryAgentProvider.cs` exists
- `tests/OpenClawNet.UnitTests/Models/AzureOpenAIAgentProviderTests.cs` exists

**AgentProfile Entity:**
- Entity at `src/OpenClawNet.Storage/Entities/AgentProfile.cs` already has Foundry fields (`ProjectId`, `DeploymentName`)

**Conclusion:** Complete. Migration already applied in earlier work.

---

### PR #3: Foundry Local Bridge + GitHub Copilot Provider
**Status:** ✅ **COMPLETE**

**Evidence:**
- `src/OpenClawNet.Models.FoundryLocal/FoundryLocalAgentProvider.cs` exists (wraps legacy client)
- `src/OpenClawNet.Models.GitHubCopilot/GitHubCopilotAgentProvider.cs` exists
- Tests exist for both

**Conclusion:** Complete.

---

### PR #4: RuntimeAgentProvider Gateway + DI Registration
**Status:** ✅ **COMPLETE**

**Evidence:**
- `src/OpenClawNet.Gateway/Services/RuntimeAgentProvider.cs` exists
- All 5 providers registered in `Program.cs` lines 86-99
- Routing tests exist at `tests/OpenClawNet.UnitTests/Services/RuntimeAgentProviderTests.cs`

**DI Registration (Program.cs:86-99):**
```csharp
builder.Services.AddSingleton<OllamaAgentProvider>();
builder.Services.AddSingleton<IAgentProvider>(sp => sp.GetRequiredService<OllamaAgentProvider>());
// ... (repeated for Azure OpenAI, Foundry, FoundryLocal, GitHubCopilot)
builder.Services.AddSingleton<RuntimeAgentProvider>();
builder.Services.AddSingleton<IAgentProvider>(sp => sp.GetRequiredService<RuntimeAgentProvider>());
```

**Conclusion:** Complete. Phase 1 (Provider Infrastructure) is 100% done.

---

### PR #5: MAF Runtime Cutover
**Status:** 🟡 **PARTIAL** — `DefaultAgentRuntime` uses MAF `ChatClientAgent` for skills enrichment, but still runs manual tool loop

**Current State:**
- `src/OpenClawNet.Agent/DefaultAgentRuntime.cs` exists
- Uses `ChatClientAgent` with `AgentSkillsProvider` for **first model call** (progressive disclosure of skills)
- Then falls back to `ModelClientChatClientAdapter` for tool iterations (manual loop)
- `UseProvidedChatClientAsIs = true` prevents MAF from handling tools automatically

**What's Missing:**
- No full cutover to `AIAgent.RunAsync()` with `FunctionInvokingChatClient`
- Manual tool loop still in place (`MaxToolIterations = 10`)
- Legacy `IModelClient` still in use (alongside `IAgentProvider`)

**Remaining Work:**
1. Change `IAgentProvider.CreateChatClient()` to return `IChatClient` wrapped with `FunctionInvokingChatClient`
2. Remove manual tool loop from `DefaultAgentRuntime`
3. Set `UseProvidedChatClientAsIs = false` to let MAF handle tools

**Estimated Effort:** 2-3 hours

**Recommendation:** **Defer to post-Jobs**. The current hybrid approach works. Full MAF cutover is not blocking Jobs implementation.

---

### PR #6: Jobs Domain Model + Migrations
**Status:** 🟡 **PARTIAL** — Entity exists, but missing new columns

**Current State:**
- `src/OpenClawNet.Storage/Entities/ScheduledJob.cs` exists with:
  - ✅ `Id`, `Name`, `Prompt`, `CronExpression`, `NextRunAt`, `LastRunAt`, `Status`, `IsRecurring`, `CreatedAt`, `StartAt`, `EndAt`, `TimeZone`, `NaturalLanguageSchedule`, `AllowConcurrentRuns`, `AgentProfileName`
  - ✅ `JobStatus` enum (Draft, Active, Paused, Cancelled, Completed)
  - ✅ `JobStatusTransitions` guard class
  - ✅ `JobRun` entity with `Id`, `JobId`, `Status`, `Result`, `Error`, `StartedAt`, `CompletedAt`

**Missing Columns (per plan):**
- `ScheduledJob.InputParametersJson` (for parameterized prompts)
- `ScheduledJob.LastOutputJson` (for chaining)
- `ScheduledJob.TriggerType` (enum: Cron, OneShot, Webhook, Manual)
- `ScheduledJob.WebhookEndpoint` (nullable, for webhook triggers)
- `JobRun.InputSnapshotJson` (input used for this run)
- `JobRun.TokensUsed` (int?)
- `JobRun.ExecutedByAgentProfile` (string?)

**Migration Needed:** Yes — add 7 new columns

**Estimated Effort:** 1 hour (entity changes + SchemaMigrator update + tests)

**Recommendation:** **START HERE**. This is the first PR with substantive new work.

---

### PR #7: JobExecutor Service + Execution Endpoints
**Status:** 🔴 **NOT STARTED**

**Current State:**
- `src/OpenClawNet.Gateway/Endpoints/JobEndpoints.cs` exists with basic CRUD:
  - ✅ `GET /api/jobs` (list)
  - ✅ `POST /api/jobs` (create)
  - ✅ `GET /api/jobs/{id}` (detail with runs)
  - ✅ `PUT /api/jobs/{id}` (update, only if Draft/Paused)
  - ✅ `DELETE /api/jobs/{id}`
- **Missing:** Action endpoints (`execute`, `start`, `pause`, `resume`)

**Missing:**
- `OpenClawNet.Runtime/Jobs/JobExecutor.cs` (new service)
- Action endpoints: `POST /api/jobs/{id}/execute`, `POST /api/jobs/{id}/start`, `POST /api/jobs/{id}/pause`, `POST /api/jobs/{id}/resume`
- `JobExecutionRequest` DTO

**Estimated Effort:** 3-4 hours

**Recommendation:** Execute after PR #6.

---

### PR #8: Dry-Run + Stats Endpoints
**Status:** 🔴 **NOT STARTED**

**Missing:**
- `POST /api/jobs/{id}/dry-run` (execute without persisting JobRun)
- `GET /api/jobs/{id}/stats` (aggregate tokens, run counts)
- `JobStatsResponse` DTO

**Estimated Effort:** 1-2 hours

**Recommendation:** Execute after PR #7.

---

### PR #9: Jobs UI — List + Create Pages
**Status:** 🔴 **NOT STARTED**

**Current State:**
- Blazor Web project exists at `src/OpenClawNet.Web`
- No Jobs UI pages exist yet

**Missing:**
- `Pages/Jobs/JobsList.razor`
- `Pages/Jobs/CreateJob.razor`
- `Services/JobsClient.cs`
- Navigation link in `Shared/NavMenu.razor`

**Estimated Effort:** 3-4 hours

**Recommendation:** Execute after PR #8.

---

### PR #10: Jobs UI — Detail + RunDetail Pages
**Status:** 🔴 **NOT STARTED**

**Missing:**
- `Pages/Jobs/JobDetail.razor`
- `Pages/Jobs/JobRunDetail.razor`

**Estimated Effort:** 2-3 hours

**Recommendation:** Execute after PR #9.

---

### PR #11: Legacy Code Cleanup
**Status:** 🔴 **NOT STARTED**

**Current State:**
- `IModelClient` still exists at `src/OpenClawNet.Models.Abstractions/IModelClient.cs`
- Used alongside `IAgentProvider` in hybrid runtime

**Recommendation:** **Defer to post-Jobs**. No urgent need to delete legacy code until full MAF cutover is complete.

---

### PR #12: Telemetry + Documentation Finalization
**Status:** 🔴 **NOT STARTED**

**Missing:**
- GenAI OTel spans for Job execution
- `/docs/architecture/skills.md`
- `/docs/architecture/jobs.md` (partial — needs execution sections)
- README screenshots

**Estimated Effort:** 2-3 hours

**Recommendation:** Execute after PR #10.

---

## Rollout Plan Revision

### Recommended Sequence

1. **PR #6: Jobs Domain Model + Migrations** (NEW WORK — 1 hour) ⬅️ **START HERE**
2. **PR #7: JobExecutor + Action Endpoints** (NEW WORK — 3-4 hours)
3. **PR #8: Dry-Run + Stats Endpoints** (NEW WORK — 1-2 hours)
4. **PR #9: Jobs UI — List + Create** (NEW WORK — 3-4 hours)
5. **PR #10: Jobs UI — Detail + RunDetail** (NEW WORK — 2-3 hours)
6. **PR #12: Telemetry + Docs** (NEW WORK — 2-3 hours)
7. **Deferred:** PR #5 (MAF Runtime Cutover), PR #11 (Legacy Cleanup)

### Total Effort Estimate
**Core Jobs Implementation (PR #6-#10):** ~12-16 hours  
**With Telemetry/Docs (PR #12):** ~14-19 hours

---

## Decision Drop Summary

**For:** `.squad/decisions/inbox/ripley-implementation-delta.md`

**Verdict:**
- PRs #1-#4 are complete (providers + routing infrastructure)
- PR #5 is partial but deferrable (MAF cutover not blocking)
- **First real work:** PR #6 (Jobs Domain Model)
- Clean execution path: #6 → #7 → #8 → #9 → #10 → #12
- Estimated delivery: 2-3 days of focused work

**Recommendation:** Execute PR #6 immediately.
