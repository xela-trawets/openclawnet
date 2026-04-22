# 🎤 Session 4: Speaker Script — Automation + Cloud (SERIES FINALE)

**Duration:** 50 minutes | **Presenters:** Bruno (lead) + Co-presenter (demos + audience)

---

## Pre-Session Checklist (10 min before)

- [ ] `aspire run` — verify full app is running (Session 3 complete)
- [ ] Blazor chat UI functional with skills and memory working
- [ ] `dotnet test` passes all 24 tests (23 unit + 1 integration)
- [ ] If Azure available: verify Azure OpenAI endpoint responds
- [ ] Open `ToolRegistryTests.cs` in VS Code for Copilot Moment
- [ ] Have Aspire Dashboard URL ready
- [ ] Prepare health check: `curl http://localhost:5000/health`
- [ ] Have the full platform demo flow rehearsed (7-step walkthrough)

---

## Timeline

### 0:00–2:00 — Opening (2 min)

**Bruno:**
> "Welcome to the series finale! Over three sessions we built a chatbot, gave it tools with security, added personality through skills and memory through summarization. Today we take it to production: cloud providers, automated scheduling, testing, and a full platform demo."

- Show the journey slide: Session 1 ✅ → Session 2 ✅ → Session 3 ✅ → Session 4 🎉
- *"Three production challenges: cloud providers, automation, and testing."*

**Co-presenter:**
- Confirm audience visibility
- Set the energy: *"This is the finale — we're going to see EVERYTHING come together."*

---

### 2:00–14:00 — Stage 1: Cloud Providers (12 min)

**Bruno (concepts + code, 12 min):**

**[0:02–0:04] Why cloud? (2 min)**
- Show local LLMs vs Cloud comparison table
- *"Local LLMs like Ollama and Foundry Local are perfect for dev — free, local. But production needs GPT-4o quality, 99.9% SLA, team-wide access, and compliance."*

**[0:04–0:07] IModelClient polymorphism (3 min)**
- Show the `IModelClient` interface — four members: ProviderName, CompleteAsync, StreamAsync, IsAvailableAsync
- *"One interface, three implementations. Your agent code doesn't change — only the DI registration."*
- This is the Strategy pattern in action

**[0:07–0:10] AzureOpenAIModelClient code (3 min)**
- Walk through the class: 137 LOC, uses Azure.AI.OpenAI SDK
- `MapMessages()` — converts OpenClawNet ChatMessage to SDK types
- Streaming with `IAsyncEnumerable` — same pattern as the local LLM client
- Configuration via `AzureOpenAIOptions`: Endpoint, ApiKey, DeploymentName

**[0:10–0:12] FoundryModelClient code (2 min)**
- Walk through: 195 LOC, raw HttpClient, custom DTOs
- `BuildPayload()` — manual JSON construction
- `EnsureConfigured()` — validates endpoint/key before calls
- *"Different API shape — that's why the abstraction matters."*

**[0:12–0:14] DI switching (2 min)**
- Show the three options in `Program.cs`:
  - `services.AddOllama()` / `services.AddAzureOpenAI()` / `services.AddFoundry()`
- *"Same agent code. Same tools. Same skills. Different cloud."*

### [DEMO] Provider Switching (~minute 12)

**Co-presenter demonstrates:**

1. Show current chat working with local LLM
2. **If Azure available:** Switch DI to Azure OpenAI, restart, show same chat with cloud provider
3. Compare: same interface, same tools, different model quality
4. Point out: response quality, speed difference

**[FALLBACK] If no Azure available:**
- Show the `AzureOpenAIOptions` configuration in `appsettings.json`
- Show the DI line that would switch it
- *"The code is identical — you just change one line and provide credentials."*
- Show `IsAvailableAsync()` returning false for unconfigured providers

---

### 14:00–26:00 — Stage 2: Scheduling + Health (12 min)

**Bruno (concepts + code, 12 min):**

**[0:14–0:17] BackgroundService pattern (3 min)**
- *"ASP.NET Core's BackgroundService runs tasks alongside your web app. No separate process needed."*
- Show `JobSchedulerService` code — `ExecuteAsync` with while loop
- Polls every 30 seconds for due jobs
- Uses same `IAgentOrchestrator` as the chat — full tool/skill access

**[0:17–0:20] JobSchedulerService in detail (3 min)**
- Walk through the flow diagram: Wait 30s → Query due jobs → Execute each → Update status → Loop
- Creates `JobRun` records for audit trail
- Graceful shutdown via `CancellationToken`
- *"Your agent works while you sleep."*

**[0:20–0:22] SchedulerTool — the agent schedules itself (2 min)**
- Three actions: create, list, cancel
- User says "Remind me every morning at 9 AM" → agent calls schedule tool → cron job created
- *"A tool that creates jobs. A service that runs them. Full circle."*

**[0:22–0:26] ServiceDefaults — Health + Telemetry (4 min)**
- Show the one-liner: `builder.AddServiceDefaults()`
- What you get:
  - `GET /health` — readiness probe (DB, model provider)
  - `GET /alive` — liveness probe
  - OpenTelemetry: ASP.NET Core metrics, HttpClient metrics, runtime metrics
  - OTLP exporter for Aspire Dashboard
  - Service discovery, resilience handlers

### [DEMO] Scheduling + Health (~minute 24)

**Co-presenter demonstrates:**

1. Show the Aspire Dashboard — all services visible with health status
2. Chat: "Schedule a reminder in 5 minutes to check the weather"
   - Show the agent calling the schedule tool
   - Show the job created in the database
3. Hit `GET /health` — show the JSON response with component statuses
4. Point out OpenTelemetry traces in the Aspire Dashboard

**[FALLBACK]** If Aspire Dashboard not available:
- Show the `GET /health` response in terminal
- Show the schedule tool response from the chat
- *"In production, Aspire Dashboard gives you traces, metrics, and logs in one view."*

---

### 26:00–38:00 — Stage 3: Testing + Production (12 min)

**Bruno (testing patterns, 12 min):**

**[0:26–0:28] Test pyramid overview (2 min)**
- Show the test pyramid diagram: 23 unit, 1 integration, E2E manual
- *"You can't ship what you can't test."*
- What we mock: IModelClient, IDbContextFactory, ITool, ISkillLoader

**[0:28–0:31] PromptComposerTests (3 min)**
- 4 tests: system prompt presence, skill injection, session summary, conversation history
- Show `ComposeAsync_IncludesActiveSkills` — FakeSkillLoader + assertion
- *"The system prompt is the most critical piece — we test it thoroughly."*

**[0:31–0:33] ToolExecutorTests (2 min)**
- 3 tests: tool not found, successful execution, batch execution
- Show the approval policy enforcement test
- *"Every failure path is covered."*

**[0:33–0:35] SkillParserTests + ConversationStoreTests (2 min)**
- SkillParser: 4 tests — valid frontmatter, no frontmatter, disabled flag, empty content
- ConversationStore: 7 tests — CRUD operations with in-memory EF Core
- *"Edge cases matter — malformed skill files shouldn't crash the system."*

**[0:35–0:36] ToolRegistryTests (1 min)**
- 5 tests: register, case-insensitive lookup, not found, get all, manifest
- Set up context for the Copilot moment

### [DEMO] Run Tests (~minute 34)

**Co-presenter:**

```bash
dotnet test --verbosity normal
```

1. Show all 24 tests passing
2. Point out test categories and naming patterns
3. *"23 unit + 1 integration = green across the board."*

**[FALLBACK]** If tests fail:
- Show pre-captured passing test output
- Investigate after the session

### 🤖 Copilot Moment (~minute 36)

**Co-presenter leads, Bruno narrates:**

> "Let's write a new test with Copilot — we'll add test #25."

1. Open `ToolRegistryTests.cs`
2. Open Copilot Chat, type the prompt (see [copilot-prompts.md](./copilot-prompts.md))
3. Accept the generated test
4. Run `dotnet test` — now 25 pass
5. *"Copilot reads the existing test patterns and generates one that matches perfectly."*

**[FALLBACK]** If Copilot doesn't generate valid test:
- Explain the pattern: Arrange (two FakeTools, same name) → Act (register both) → Assert (GetTool returns second)
- *"The pattern is always the same: Arrange, Act, Assert."*

---

### 38:00–50:00 — Series Finale Closing (12 min)

**Bruno leads the finale:**

### [DEMO] Full Platform Demo (5 min, ~minute 38)

> "Let's see everything we built — all four sessions — working together."

Walk through the complete platform end-to-end:

1. **Start Aspire** — show all services coming up in the dashboard
2. **Chat** — "Hello OpenClaw!" → streaming response **(Session 1)**
3. **Tool use** — "What files are in the skills/ directory?" → FileSystem tool call **(Session 2)**
4. **Skill toggle** — Enable code-review skill, ask a code question **(Session 3)**
5. **Schedule** — "Remind me in 5 minutes to check the tests" → schedule tool **(Session 4)**
6. **Health** — `GET /health` → all components healthy **(Session 4)**
7. **Dashboard** — Show Aspire with traces, metrics, all services green

*"From zero to a complete AI agent platform — in four sessions."*

### Series Recap (3 min, ~minute 43)

**Bruno:**
- Show the four-session recap table
- Show the full architecture diagram
- Walk through each layer: UI → Agent → Tools/Skills/Memory/Scheduler → Models → Storage
- *"Every box is a real project. Every arrow is a real dependency."*

### Where to Go from Here (2 min, ~minute 46)

- **Custom tools:** Jira, GitHub, Slack integrations
- **Domain skills:** Specialized skill packs for your team
- **Azure deployment:** Container Apps with Aspire
- **Advanced memory:** RAG with vector search
- **Multi-agent:** Agent-to-agent communication
- **GitHub Copilot:** Use Copilot to extend the platform itself

### Thank You + Q&A (2 min, ~minute 48)

**Bruno:**
> "Thank you for joining us through all four sessions. You've built a production-ready AI agent platform with .NET 10, Aspire, and GitHub Copilot. The repo is open — star it, fork it, break it, fix it."

**Co-presenter:**
- Share repo link: `github.com/elbruno/openclawnet`
- *"Built with: .NET 10, Aspire, GitHub Copilot, Local LLMs (Ollama / Foundry Local)"*
- Open the floor for Q&A
- 🎉 Celebrate with the audience!

---

## Key Talking Points (Quick Reference)

- **IModelClient polymorphism** — one interface, swap providers without touching agent code
- **BackgroundService** — your agent works while you sleep, polls every 30s
- **ServiceDefaults** — one call gets health checks, telemetry, service discovery
- **Test pyramid** — 24 tests prove the architecture works, all mockable interfaces
- **Full circle** — chat (S1) → tools (S2) → skills/memory (S3) → cloud/scheduling/tests (S4)
- **Series finale energy** — this is the celebration of what we built together! 🎉
