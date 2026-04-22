# 🎤 Session 3: Speaker Script — Skills + Memory

**Duration:** 50 minutes | **Presenters:** Bruno (lead) + Co-presenter (demos + audience)

---

## Pre-Session Checklist (10 min before)

- [ ] `aspire run` — verify Session 2 app is running end-to-end
- [ ] Prepare a test skill file for live demo (e.g., `security-auditor.md`)
- [ ] Navigate to Skills page — toggle one skill to confirm loading works
- [ ] Pre-run 20+ messages in a conversation to have summarization data ready
- [ ] Have sample skills ready to display: `skills/built-in/dotnet-expert.md`
- [ ] Open `MemoryEndpoints.cs` in VS Code for Copilot Moment #2
- [ ] Verify skills API: `curl http://localhost:5000/api/skills`

---

## Timeline

### 0:00–2:00 — Opening (2 min)

**Bruno:**
> "Session 2 gave our agent hands — it can read files, run commands, fetch web pages. But every user gets the same generic experience. Today we fix that. We give the agent **personality** through skills and **long-term memory** through summarization."

- Show Sessions 1–2 recap: Foundation ✅, Tools ✅, Security ✅
- Transition: *"Same agent, different behavior. No code changes — just Markdown files."*

**Co-presenter:**
- Confirm audience can see slides
- Quick check: "Everyone's Session 2 working?" (thumbs up/down)

---

### 2:00–14:00 — Stage 1: Skill System (12 min)

**Bruno (concepts + code, 12 min):**

**[0:02–0:04] What is a skill? (2 min)**
- *"A Markdown file with YAML frontmatter. No code changes needed."*
- Show `dotnet-expert.md` example — YAML frontmatter fields: name, description, tags, enabled
- Content after `---` is pure Markdown — the agent's behavior instructions
- *"Your product manager could write a skill. Your QA engineer could write one."*

**[0:04–0:06] SkillDefinition and SkillContent models (2 min)**
- `SkillDefinition` — immutable record for UI: name, description, tags, enabled, filePath
- `SkillContent` — what the prompt composer uses: name, content, description, tags
- *"Sealed records for immutability. Different types for different consumers."*

**[0:06–0:09] FileSkillLoader implementation (3 min)**
- Scans `skills/built-in/` and `skills/samples/` directories for `*.md` files
- Thread-safe with lock for `_disabledSkills` HashSet
- Graceful error handling — malformed files skipped with a warning
- `EnableSkill` / `DisableSkill` — just HashSet add/remove
- `ReloadAsync` — re-scan directory without restart

**[0:09–0:11] SkillParser — YAML extraction (2 min)**
- Regex pattern: `^---\s*\n(.*?)\n---\s*\n(.*)$`
- Group 1: YAML frontmatter → deserialized into SkillDefinition fields
- Group 2: Markdown content → the actual behavior instructions
- Static utility — no state, no DI needed

**[0:11–0:14] How skills weave into the system prompt (3 min)**
- Show `DefaultPromptComposer.ComposeAsync()` code
- Three lines: get active skills → format as Markdown sections → append to system prompt
- *"The LLM sees skill content as instructions. More skills = longer prompt = more tokens."*
- Tool definitions are separate (via model API), skills are in the system prompt

### 🤖 Copilot Moment #1 (~minute 10)

**Co-presenter leads, Bruno narrates:**

> "Let's create a brand-new skill file from scratch using Copilot."

1. Open Copilot Chat
2. Type the prompt (see [copilot-prompts.md](./copilot-prompts.md) → Prompt 1)
3. Accept the generated `security-auditor.md` file
4. Save to `skills/samples/security-auditor.md`
5. Reload skills via API: `curl -X POST http://localhost:5000/api/skills/reload`
6. Verify it appears: `curl http://localhost:5000/api/skills`

**[FALLBACK]** If Copilot doesn't generate valid YAML frontmatter:
- Show a pre-prepared `security-auditor.md` file
- Paste it manually, reload, verify
- *"The format is simple — anyone can create one by following the pattern."*

---

### 14:00–29:00 — Stage 2: Memory & Summarization (15 min)

**Bruno (concepts + code, 15 min):**

**[0:14–0:17] The context window problem (3 min)**
- *"LLMs have token limits — 8K to 128K. Long conversations fill up fast."*
- More tokens = higher cost (even local models get slower)
- Naive truncation = lost context — the agent forgets important details
- *"We need a smarter approach."*

**[0:17–0:20] Summarization strategy (3 min)**
- Show the strategy table:
  - Recent messages (last N): keep **verbatim**
  - Older messages: **summarize** into key points
  - Very old: available via **semantic search**
- Summary injected at top of system prompt
- Triggered automatically based on message count

**[0:20–0:23] DefaultMemoryService code (3 min)**
- Uses `IDbContextFactory<OpenClawDbContext>` — correct pattern for async services
- `GetSessionSummaryAsync` — returns most recent summary
- `StoreSummaryAsync` — persists new `SessionSummary` entity
- `GetStatsAsync` — returns TotalMessages, SummaryCount, CoveredMessages, LastSummaryAt
- *"MemoryStats gives the UI transparency into what the memory system is doing."*

**[0:23–0:26] DefaultEmbeddingsService code (3 min)**
- Backed by `Elbruno.LocalEmbeddings` — ONNX models, runs locally
- `EmbedAsync` — text → embedding vector
- `CosineSimilarity` — dot product / (magnitude1 × magnitude2)
- *"Find past conversations about 'dependency injection' even if the user said 'IoC container'."*
- Key point: no API calls, no data leaves the machine

**[0:26–0:29] SessionSummary entity (3 min)**
- Show the entity: Id, SessionId, Summary, CoveredMessageCount, CreatedAt
- One session → many summaries (as conversation grows)
- Cascade-deletes with the parent session

### [DEMO] Summarization Trigger (~minute 25)

**Co-presenter demonstrates:**

1. Show a conversation with 20+ messages in the Blazor UI
2. Check memory stats: `curl http://localhost:5000/api/memory/{sessionId}/stats`
3. Point out: TotalMessages, SummaryCount, CoveredMessages
4. Send a few more messages to push past summarization threshold
5. Re-check stats — show SummaryCount increased

**[FALLBACK]** If summarization hasn't triggered:
- Show pre-captured stats output showing before/after
- Explain the threshold and trigger mechanism
- *"The summarization runs automatically — no user action needed."*

---

### 29:00–44:00 — Stage 3: Integration + UI (15 min)

**Bruno (endpoints + integration, 15 min):**

**[0:29–0:32] SkillEndpoints walkthrough (3 min)**
- Show `SkillEndpoints.cs` — four Minimal API endpoints
- `GET /api/skills` — list all skills with metadata
- `POST /api/skills/reload` — re-scan directory
- `POST /api/skills/{name}/enable` / `disable` — toggle at runtime
- *"No restart required. The UI calls these when you click the toggle."*

**[0:32–0:35] MemoryEndpoints walkthrough (3 min)**
- Show `MemoryEndpoints.cs` — three read-only endpoints
- `GET /api/memory/{sessionId}/summary` — latest summary
- `GET /api/memory/{sessionId}/summaries` — all summaries
- `GET /api/memory/{sessionId}/stats` — dashboard data
- *"Stats gives the UI everything it needs to render a memory dashboard."*

### [DEMO] Skill Toggle — Before/After (~minute 35)

**Co-presenter demonstrates:**

1. **Enable** `dotnet-expert` skill: `curl -X POST http://localhost:5000/api/skills/dotnet-expert/enable`
2. Chat: "What's the best way to handle DI in .NET?"
3. → Expert response with .NET-specific patterns, Microsoft docs references
4. **Disable** `dotnet-expert`: `curl -X POST http://localhost:5000/api/skills/dotnet-expert/disable`
5. Chat: same question
6. → Generic response about dependency injection
7. *"Same agent. Same model. Different behavior. Just a Markdown file."*

**[FALLBACK]** If skill toggle doesn't visibly change responses:
- Show pre-recorded side-by-side comparison
- Explain: "With smaller models the difference may be subtle, but with GPT-4o it's dramatic"

### [DEMO] Memory Stats Panel (~minute 38)

**Co-presenter:**
1. Show the Blazor UI memory stats component
2. Point out: total messages, summary count, last summary time
3. *"Full transparency — users see exactly what the memory system is doing."*

### 🤖 Copilot Moment #2 (~minute 40)

**Co-presenter leads, Bruno narrates:**

> "Let's add date filtering to the memory API — a real feature request."

1. Open `MemoryEndpoints.cs`
2. Open Copilot Chat, type the prompt (see [copilot-prompts.md](./copilot-prompts.md) → Prompt 2)
3. Accept the suggestion — new endpoint with `from` and `to` query parameters
4. Test: `curl "http://localhost:5000/api/memory/{sessionId}/summaries?from=2025-01-01T00:00:00Z&to=2025-12-31T23:59:59Z"`

**[FALLBACK]** If Copilot doesn't generate the expected endpoint:
- Manually add the date-filtered endpoint
- *"The pattern is the same Minimal API style — Copilot matches existing conventions."*

---

### 44:00–50:00 — Closing (6 min)

**Bruno:**

**[0:44–0:46] Key insight (2 min)**
> "Skills are just markdown. Memory is transparent."
- Anyone can create a skill — no C# required
- Users see what's summarized, not a black box
- Developers get clean abstractions: `ISkillLoader`, `IMemoryService`

**[0:46–0:48] What we built checklist (2 min)**
- ✅ Skill system: YAML + Markdown → agent behavior
- ✅ FileSkillLoader: scan, parse, enable/disable at runtime
- ✅ DefaultPromptComposer: skills woven into system prompt
- ✅ DefaultMemoryService: summarization with DB persistence
- ✅ DefaultEmbeddingsService: local semantic search
- ✅ Skills API + Memory API endpoints
- ✅ 2 Copilot moments: skill file + date filter

**[0:48–0:50] Session 4 preview (2 min)**
> "Our agent has personality and memory. Next session: we take it to the cloud."

- Cloud providers (Azure OpenAI, Foundry)
- Job scheduling with cron expressions
- Health checks and testing
- **Series finale** — the full platform demo

**Co-presenter:**
- Share repo link: `github.com/elbruno/openclawnet`
- Remind: check out `session-3-complete` tag
- Thank audience, invite questions

---

## Key Talking Points (Quick Reference)

- **Skills = Markdown files** — no code, no deploy, no restart
- **Prompt composition** — base instructions + active skills + session summary + history
- **Context window management** — summarize old messages, keep recent ones verbatim
- **Local embeddings** — semantic search with no API calls, data stays on machine
- **Transparency** — users see memory stats, not a black box
