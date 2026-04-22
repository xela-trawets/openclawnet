# Session 1: Speaker Script

## Presenters

- **Bruno** (Lead): Architecture explanations, code walkthroughs, Copilot demos
- **Pablo** (Co-presenter): Live demos, audience engagement, chat monitoring, Q&A facilitation

## Timeline

| Time | Min | Who | Activity | Notes |
|------|-----|-----|----------|-------|
| 0:00 | 2 | Bruno | Welcome + series overview | Share repo link in chat: `github.com/elbruno/openclawnet`. Solution: `OpenClawNet.slnx` |
| 0:02 | 2 | Bruno | **OpenClaw Origin Story** | "What is OpenClaw, the 9 pillars, why we chose .NET to implement it" |
| 0:04 | 1 | Pablo | Verify audience prerequisites | Quick poll: .NET 10? Local LLM installed? VS Code + Copilot? |
| 0:05 | 1 | Bruno | Our Tech Stack Assumptions | Five pillars: MS Extensions for AI, Agent Framework, Aspire, .NET best practices, GitHub Copilot |
| | | | **STAGE 1: Architecture & Core Abstractions** | |
| 0:06 | 2 | Bruno | Show "Architecture at a Glance" | Display the 3-layer compact diagram from session README to set mental model |
| 0:08 | 2 | Bruno | Show architecture overview | Display `architecture-overview.svg` — explain the layer diagram |
| 0:10 | 2 | Bruno | Solution structure walkthrough | Display `solution-structure.svg` — 27 projects by responsibility. Solution: `OpenClawNet.slnx` |
| 0:12 | 3 | Bruno | IAgentProvider interface deep-dive | Open `IAgentProvider.cs` from `src/OpenClawNet.Models.Abstractions/` |
| 0:15 | 2 | Bruno | ChatRequest/ChatResponse records | Show immutable record pattern, explain why records > classes |
| 0:17 | 1 | Bruno | DI wiring in Gateway Program.cs | Show `builder.Services.AddSingleton<IAgentProvider, OllamaAgentProvider>()` registration |
| 0:18 | 1 | Bruno | OllamaAgentProvider as reference | Quick walk of class structure from `src/OpenClawNet.Models.Ollama/` |
| 0:19 | 2 | Pablo | **Checkpoint 1**: Open files in VS Code | Open `IAgentProvider.cs` + `OllamaAgentProvider.cs` side by side |
| 0:21 | 3 | Bruno | 🤖 **Copilot Moment #1**: Generate FoundryLocalAgentProvider | Copilot Chat: generate provider from IAgentProvider + OllamaAgentProvider pattern |
| | | | **STAGE 2: Local LLMs + Workspace + Storage** | |
| 0:24 | 2 | Bruno | What are Local LLMs? | Ollama / Foundry Local — no API keys, no cloud costs, REST at localhost |
| 0:26 | 3 | Bruno | OllamaAgentProvider.CreateChatClient walkthrough | Microsoft Agent Framework integration + `IChatClient` pipeline |
| 0:29 | 1 | Pablo | **Checkpoint 3**: Local LLM verification | Terminal: `ollama list`, `curl http://localhost:11434/api/tags` |
| 0:30 | 2 | Bruno | **Workspace bootstrap files** | Location: `src/OpenClawNet.Agent/workspace/` — files: AGENTS.md, SOUL.md, USER.md |
| 0:32 | 6 | Pablo | 🎬 **DEMO 3: Agent Personality Swap** | Edit `src/OpenClawNet.Agent/workspace/AGENTS.md` live, swap with files from `docs/sessions/session-1/demo-agents/`. See `demo-scripts.md` |
| 0:36 | 1 | Pablo | Open IWorkspaceLoader.cs | Set up Copilot Moment #2 — show the interface contract |
| 0:37 | 3 | Bruno | 🤖 **Copilot Moment #2**: Generate IWorkspaceLoader impl | Copilot Chat: implement WorkspaceLoader from interface + AGENTS.md concept. Note: actual type is `BootstrapContext`, not `WorkspaceContext` |
| 0:40 | 2 | Bruno | Storage entities overview | Display `entity-relationship.svg` — 7 entities explained |
| 0:42 | 1 | Bruno | ConversationStore repository | Show CRUD methods, explain repository pattern |
| 0:43 | 3 | Bruno | 🤖 **Copilot Moment #3**: Complete repo method | Type `GetRecentSessionsAsync` signature → Copilot completes LINQ inline |
| | | | **STAGE 3: Gateway + HTTP NDJSON + Blazor** | |
| 0:46 | 2 | Bruno | Minimal API endpoint map | Show the REST endpoints including `/api/chat/stream`, explain static extension pattern |
| 0:48 | 3 | Bruno | Settings endpoint (SettingsEndpoints.cs) | Review GET/PUT /api/settings for runtime provider switching |
| 0:51 | 2 | Bruno | HTTP NDJSON streaming (ChatStreamEndpoints) | Walk through `POST /api/chat/stream`, NDJSON response format, error handling |
| 0:53 | 8 | Pablo | 🎬 **DEMO 1: Provider Switch (Ollama → Azure)** | Model Providers page → configure provider → send message → Aspire traces show new endpoint. **Point out agent selector badge** with provider and model info. See `demo-scripts.md` |
| 1:01 | 1 | Bruno | Agent selector badge | Inline display of active provider and model in the chat UI |
| 1:02 | 1 | Bruno | Model Providers + Agent Profiles pages | `/model-providers` for provider definitions, `/agent-profiles` for named agent configurations |
| 1:03 | 1 | Bruno | AppHost orchestration | Show 18-line Aspire topology — `WithReference`, `WaitFor` |
| 1:04 | 3 | Pablo | 🎬 **Checkpoint 6: Full Stack Demo** | `aspire run` → Aspire Dashboard → Blazor UI → **highlight agent selector badge** → send message → DevTools Network (show HTTP POST to `/api/chat/stream`, NDJSON) |
| 1:07 | 10 | Both | 🎬 **DEMO 2: Copilot CLI + Aspire Error Fix** | Trigger error → Copilot CLI reads Aspire traces → suggests fix → apply → refresh. See `demo-scripts.md` |
| | | | **CLOSING** | |
| 1:17 | 2 | Bruno | Recap + end-to-end flow | Trace the full path: browser → HTTP POST → `/api/chat/stream` → orchestrator → workspace → provider (Ollama/Azure/Copilot) → NDJSON tokens back → agent selector badge |
| 1:19 | 1 | Bruno | Session 2 preview | "Next: give the agent superpowers — tools, the agent loop, and real-world actions" |
| 1:20 | 1 | Pablo | Resources + links | Post in chat: repo, Aspire docs, Ollama, Azure OpenAI, GitHub Copilot SDK |
| 1:21 | 6 | Both | Q&A | Bruno answers technical questions; Pablo manages chat queue and queues questions |

> **Total: ~87 minutes** (81 min content + 6 min Q&A)  
> **With Demo Compression:** Skip Demo 2 or Demo 3 to reduce to ~70 minutes (see "If Running Over" section)

---

## Demo Checkpoints

### Checkpoint 1: Solution Overview (min 7–10)

**What should be ready:**
- VS Code open with the full solution loaded (`OpenClawNet.slnx` from repo root)
- Solution Explorer visible, all 27 projects visible
- `architecture-overview.svg` and `solution-structure.svg` ready to display (browser tab or slide)
- Copilot Chat panel accessible (Ctrl+Shift+I)

**Steps:**
1. Open VS Code with the `OpenClawNet.slnx` solution from the repo root
2. Open the Solution Explorer panel — walk top-to-bottom through the 27 projects
3. Call out the grouping: Models, Tools, Storage, Skills, Memory, Agent, Gateway, Web, AppHost
4. Open `architecture-overview.svg` in the browser — point to each layer
5. Say: *"27 projects. About 4,200 lines of code total. Each project does exactly one thing."*

**Fallback:** If VS Code is slow to load or the solution doesn't restore cleanly, display the pre-rendered `architecture-overview.svg` and `solution-structure.svg` diagrams directly in the slides and narrate from them.

---

### Checkpoint 2: Copilot Moment #1 — Generate Model Provider (min ~19)

**What should be ready:**
- `src/OpenClawNet.Models.Abstractions/IAgentProvider.cs` open in VS Code
- `src/OpenClawNet.Models.Ollama/OllamaAgentProvider.cs` open in VS Code (second tab)
- GitHub Copilot extension active and authenticated (check bottom-left status bar)
- Copilot Chat panel open (Ctrl+Shift+I)

**Steps:**
1. Pablo opens both files — one tab each, arrange side by side if possible
2. Bruno navigates to `IAgentProvider.cs`: *"This is the contract. Two main methods: CreateChatClient to get an IChatClient instance, and IsAvailableAsync for health checks."*
3. Navigate to `OllamaAgentProvider.cs`: *"And this is one concrete implementation. Notice the patterns — it creates an IChatClient configured for Ollama, uses the Microsoft Agent Framework abstractions."*
4. Switch to Copilot Chat panel
5. In the chat input, reference both files:
   ```
   #IAgentProvider.cs #OllamaAgentProvider.cs
   ```
6. Type the exact prompt:
   ```
   Using OllamaAgentProvider as a reference pattern, implement FoundryLocalAgentProvider
   using Microsoft.AI.Foundry.Local — match the CreateChatClient method signature from
   IAgentProvider exactly. Follow the same pattern for creating and configuring the
   IChatClient instance.
   ```
   ```
7. Wait for generation (~10–20 seconds)
8. Review output — highlight: correct interface implementation, proper `CreateChatClient` method, integration with Foundry Local SDK
9. Say: *"This is what clean interface design enables — Copilot sees the contract plus one example and extrapolates a complete, working implementation. The IAgentProvider interface is the Microsoft Agent Framework integration point."*

**Expected output:** A complete `FoundryLocalAgentProvider.cs` class (~80–120 LOC) that correctly implements the `IAgentProvider` interface using `Microsoft.AI.Foundry.Local`.

**Fallback:** If Copilot is unresponsive or the output is obviously wrong, open `src/OpenClawNet.Models.FoundryLocal/FoundryLocalAgentProvider.cs` which already exists in the repo. Show it and say: *"This is what Copilot would generate — let me walk you through the key parts."* Then narrate the actual file.

---

### Checkpoint 3: Local LLM Verification (min ~27)

**What should be ready:**
- A terminal open (VS Code integrated terminal or separate window)
- Ollama installed and previously started (`ollama serve` running as a service or background process)
- `llama3.2` model already pulled

**Steps (Pablo runs, Bruno narrates):**
1. Run:
   ```bash
   ollama list
   ```
   Expected output shows `llama3.2` with size and modification date.
2. Run:
   ```bash
   curl http://localhost:11434/api/tags
   ```
   Expected output: JSON with `"models"` array including `llama3.2`.
3. Bruno says: *"No API key. No cloud account. No cost. The model is right there, on your machine, ready to answer questions."*

**Fallback — Ollama not running:**
```bash
ollama serve
# wait 10 seconds, then retry ollama list
```

**Fallback — Model not pulled:**
```bash
ollama pull llama3.2
# ~2–3 minutes for the 2GB model — do this BEFORE the session
```

**Fallback — Using Foundry Local instead:**
```bash
foundrylocal model list
foundrylocal model download --assigned phi-4
```

**Pre-session requirement:** Verify this checkpoint works at least 30 minutes before the session starts.

---

### Checkpoint 4: Copilot Moment #2 — Generate Workspace Loader (min ~31) ⭐ NEW

**What should be ready:**
- `src/OpenClawNet.Agent/IWorkspaceLoader.cs` open in VS Code
- Copilot Chat panel open
- The audience has just heard the explanation of AGENTS.md / SOUL.md / USER.md

**Context to set (Bruno, ~30 seconds before opening Copilot):**
> *"Workspace bootstrap files are the secret to no-code agent customization. AGENTS.md changes behavior. SOUL.md adds guardrails. USER.md personalizes responses. The WorkspaceLoader reads these at session start and injects them into the system prompt. The return type is BootstrapContext, not WorkspaceContext — that's the actual record that holds all three file contents. Let's see if Copilot can implement this loader from the interface contract alone."*

**Steps:**
1. Pablo opens `src/OpenClawNet.Agent/IWorkspaceLoader.cs`
2. Bruno walks the interface: *"One method — LoadAsync, takes a path, returns a BootstrapContext with all three file contents."*
3. Open Copilot Chat
4. Reference the file:
   ```
   #IWorkspaceLoader.cs
   ```
5. Type the exact prompt:
   ```
   Implement WorkspaceLoader for this interface. The workspace directory may contain
   three optional markdown files: AGENTS.md (agent behavior/persona), SOUL.md (values
   and guardrails), USER.md (user preferences). Read each file if it exists, return
   null for missing files. Return a BootstrapContext record with all three. Use
   async file I/O and respect the CancellationToken.
   ```
6. Review the output — highlight: `File.Exists` checks, `await File.ReadAllTextAsync`, nullable strings for missing files, `CancellationToken` threading through
7. Say: *"The magic here isn't the code — it's the concept. By injecting these files into the system prompt at session start, the agent's entire personality, values, and understanding of the user come from plain markdown files that anyone can edit."*

**Expected output:** ~30–50 LOC implementing `WorkspaceLoader` with safe async file reads for all three optional files.

**Fallback:** If Copilot is unresponsive, open `src/OpenClawNet.Agent/WorkspaceLoader.cs`. Show the actual implementation and walk through the three file reads: *"This is what Copilot generates when the interface design is clean — the method name, parameter types, and return type encode everything it needs to know."*

---

### Checkpoint 5: Copilot Moment #3 — Repo Method (min ~37)

**What should be ready:**
- `src/OpenClawNet.Storage/ConversationStore.cs` open in VS Code
- Cursor positioned after the last method in the class, on a new blank line
- Copilot inline suggestions enabled (check: `Ctrl+Shift+P` → "GitHub Copilot: Enable")

**Steps:**
1. Navigate to the end of the `ConversationStore` class
2. Add a blank line after the last method
3. Start typing the method signature:
   ```csharp
   public async Task<List<ChatSession>> GetRecentSessionsAsync(int count = 10)
   {
   ```
4. Pause after the opening `{` — wait 2–3 seconds for the grey ghost text
5. Press **Tab** to accept
6. Review: point out `OrderByDescending(s => s.UpdatedAt)` + `Take(count)` + `ToListAsync()`
7. Say: *"The method name `GetRecentSessions`, the parameter `count`, and the return type `List<ChatSession>` — those three things gave Copilot enough signal to produce the exact LINQ chain you'd write yourself."*

**Expected output:**
```csharp
public async Task<List<ChatSession>> GetRecentSessionsAsync(int count = 10)
{
    return await _context.ChatSessions
        .OrderByDescending(s => s.UpdatedAt)
        .Take(count)
        .ToListAsync();
}
```

**Acceptable variations:**
- `AsNoTracking()` added (a bonus — mention it's a performance optimization)
- `CreatedAt` instead of `UpdatedAt` (either is reasonable)
- `CancellationToken` parameter added (even better — accept it)

**Fallback — Inline suggestion doesn't appear:**
Press `Ctrl+Space` to manually trigger. If still nothing, switch to Copilot Chat:
```
Implement the body of GetRecentSessionsAsync — return the most recent chat
sessions ordered by UpdatedAt, limited by the count parameter.
```

---

### Checkpoint 6: Full Stack Demo (min ~48)

**What should be ready:**
- No other processes running on ports 5000, 5001, 15100
- Local LLM verified (Checkpoint 3 passed)
- A terminal open at the repo root
- Chrome/Edge open for the Aspire Dashboard and Blazor UI

**Steps (Pablo drives, Bruno narrates):**

1. Run from repo root:
   ```bash
   aspire run
   ```
2. Watch the terminal — wait for both services to report healthy (~10–20 seconds)
3. Open the Aspire Dashboard URL shown in terminal (typically `https://localhost:15100`)
   - Point out the Resources tab: Gateway (Running ✅), Web (Running ✅)
   - Show health check status
   - Say: *"Two services, automatically discovered, health-checked, dashboarded — from 18 lines of AppHost code."*
4. Open Blazor UI at `http://localhost:5001`
5. Click **New Chat**
6. Type the message: *"What is Aspire and why should I use it for .NET development?"*
7. Send and watch tokens stream in real-time
   - Bruno: *"Every word that appears is a token arriving from the local LLM, flowing through IAsyncEnumerable, through the NDJSON HTTP stream, and into the browser."*
8. Open **DevTools** (F12) → **Network** tab → filter by **Fetch/XHR**
9. Find the `POST /api/chat/stream` request → **Response** tab
10. Send another message and watch the NDJSON lines appear:
    - Each line: a JSON event (`{"type":"content","content":"..."}`)
    - Rapid lines: individual token deltas
    - Final line: `{"type":"complete",...}` signals end of response
11. Say: *"This HTTP stream is NDJSON in action. Each of those tiny JSON lines is one token streamed through the IChatClient created by OllamaAgentProvider. Standard HTTP — no WebSocket needed."*

**Fallback — AppHost fails to start:**
```bash
# Run services independently
dotnet run --project src/OpenClawNet.Gateway
dotnet run --project src/OpenClawNet.Web
```
Skip the Aspire Dashboard section and proceed directly to the Blazor UI.

**Fallback — Local LLM is responding slowly:**
Have a pre-recorded screen capture or GIF of the streaming demo ready to play. Say: *"I'll show you a recording so we don't wait — but everything you're seeing was captured live on this same setup."*

**Fallback — Port conflict:**
```bash
# Kill conflicting process on Windows
netstat -ano | findstr :5001
Stop-Process -Id <PID>
# Or override the port
aspire run -- --urls http://localhost:5100
```

---

### Checkpoint 7: 🤖 Copilot Moment — Implementing an Agent Framework Skill ⭐ NEW

**When:** Bonus segment (if running under) or end of Stage 1 if time allows

**What should be ready:**
- The `skills/` directory open in VS Code Explorer
- One existing skill subdirectory visible as a reference (e.g., `skills/dotnet-code-style/`)
- Copilot Chat panel open

**Context to set (Bruno, ~30 seconds):**
> *"Skills in OpenClawNet aren't just text injections anymore. With Agent Framework, we use the agentskills.io specification — each skill lives in its own subdirectory with a SKILL.md file. The `AgentSkillsProvider` picks them up automatically. No registration, no code changes — just create the directory and file. Let's see Copilot generate one."*

**Steps:**
1. Create a new directory in `skills/`:
   ```
   skills/web-search/
   ```
2. Open Copilot Chat with the reference skill file in context:
   ```
   #skills/dotnet-code-style/SKILL.md
   ```
3. Type the prompt:
   ```
   Create a SKILL.md for a web-search skill following the agentskills.io spec.
   The skill should advertise that it can search the web and fetch URLs.
   Tools: web_search(query), web_fetch(url). Include name, description,
   version 1.0.0, and usage instructions for each tool.
   ```
4. Review the output — highlight: YAML frontmatter, tools list, usage section
5. Save as `skills/web-search/SKILL.md`
6. Say: *"The `AgentSkillsProvider` will pick this up on the next session start. No DI registration, no code changes — the skills directory is the API."*

**Expected output:** A complete `SKILL.md` file with YAML frontmatter and markdown body (~30–50 lines).

**Demo 3 — Agent Framework skills for file system access (bonus):**
- Show the existing `skills/file-system/SKILL.md`
- Explain the Advertise → Load → Execute pattern:
  1. **Advertise**: `AgentSkillsProvider` sends skill summaries to the model at the start of every turn
  2. **Load**: When the agent selects a skill, the full `SKILL.md` content is injected
  3. **Execute**: The model calls the advertised tools (`file_read`, `file_write`, `file_list`)
- Say: *"The model sees 'file-system skill available' first. Only when it decides to use files does the full skill context load. This is progressive disclosure — you don't dump everything into the prompt upfront."*

---

## Key Talking Points

### OpenClaw Origin Story (min 2–4)

> *"Before we look at the code, I want to give you one minute of context. OpenClaw is an open-source agent platform — not just a framework, an architecture reference. It defines a specific way to build agents: with a persistent gateway as the control plane, workspace-aware sessions, first-class tools, a skills system, and a pluggable model layer."*

> *"OpenClaw defines 9 pillars. And OpenClawNet maps every single one of them to a .NET type, interface, or project. This isn't just our opinion of how to build agents — we're implementing a community-defined architecture specification in .NET 10."*

> *"Why .NET? Because .NET 10 has everything we need natively: IAsyncEnumerable for streaming, Aspire for orchestration, Minimal APIs for low-overhead endpoints, HTTP NDJSON for reliable real-time push, and Blazor for the UI — all in one stack, all in C#. No JavaScript runtime needed."*

**The 9 Pillars — mention briefly, link to architecture doc:**

| # | Pillar | OpenClawNet |
|---|--------|-------------|
| 1 | Gateway as control plane | `OpenClawNet.Gateway` — persistent, stateful |
| 2 | Agent runtime with workspace | `IAgentOrchestrator` + `WorkspaceLoader` |
| 3 | Sessions and memory | `IMemoryService` + context compaction |
| 4 | First-class tools | `ITool` + FileSystem, Shell, Web, Browser |
| 5 | Skills system | `SkillLoader` + markdown with precedence |
| 6 | Model abstraction | `IAgentProvider` + fallback chain |
| 7 | Automation | Cron scheduler + webhooks |
| 8 | UI surfaces | Control UI + WebChat (Blazor) |
| 9 | Channels and nodes | `IChannel` + Teams adapter |

---

### Tech Stack Assumptions (min 5)

- *"Microsoft Extensions for AI means we code against abstractions, not specific providers. We never import `Ollama` into our business logic — only `IAgentProvider`."*
- *"The Agent Framework gives us production-tested patterns — tool calling, prompt composition, memory management."*
- *"Aspire replaces your Docker Compose file AND gives you observability from day one."*
- *"GitHub Copilot isn't just code completion — today you'll see it generate complete, working implementations from interface contracts."*

---

### Stage 1: Architecture (min 6–22)

- *"27 projects, but only ~4,200 lines of code. Each project does one thing well. That's not a coincidence — it's a deliberate constraint."*
- *"The `IAgentProvider` interface is the most important design decision in the entire platform. It means you can swap local LLMs for Azure OpenAI or any future provider without changing a single line of business logic."*
- *"Records are immutable by default — once you create a `ChatRequest`, it can't change. This makes the code predictable and thread-safe."*
- *"Notice every async method takes a `CancellationToken`. This isn't academic — when a user navigates away mid-stream, cancellation stops the inference immediately. That matters for cost and performance."*
- *[After Copilot Moment #1]* *"When your code has good structure, AI amplifies it. The interface defined the contract. The existing implementation showed the pattern. Copilot needed just those two pieces."*

---

### Stage 2: Local LLMs + Workspace + Storage (min 22–40)

**Local LLMs:**
- *"No API keys. No cloud costs. Your data never leaves the machine. For enterprise scenarios, for learning, for offline development — local LLMs are a game changer."*
- *"NDJSON streaming is simpler than SSE — each line is a complete JSON object. No event types, no retry logic. Just read a line, deserialize, yield."*
- *"`IAsyncEnumerable` is the .NET primitive for streaming data. Combined with `yield return`, it creates a natural pipeline: orchestrator loop → parse → yield → HTTP NDJSON → browser. No buffering. Tokens arrive in the browser as fast as the model generates them."*

**Workspace bootstrap files:**
- *"Here's one of the most powerful features in OpenClaw — and it requires zero code changes. Every agent session has a workspace directory at `src/OpenClawNet.Agent/workspace/`. That directory contains three optional markdown files."*
- *"`AGENTS.md` — located at `src/OpenClawNet.Agent/workspace/AGENTS.md` — controls the agent's persona and behavior. You can tell the agent to be formal or casual, focused or general, to follow specific protocols. We have pre-made sample personas ready: Pirate (`docs/sessions/session-1/demo-agents/pirate-agents.md`), Chef (`docs/sessions/session-1/demo-agents/chef-agents.md`), and Robot (`docs/sessions/session-1/demo-agents/robot-agents.md`)."*
- *"`SOUL.md` at `src/OpenClawNet.Agent/workspace/SOUL.md` — adds guardrails: values, ethical constraints, things the agent should never do. Think of it as the agent's conscience."*
- *"`USER.md` at `src/OpenClawNet.Agent/workspace/USER.md` — personalizes responses with the user's name, role, preferences, timezone, communication style."*
- *"WorkspaceLoader reads these at session start and injects them into the system prompt. Change the markdown file, start a new session, and the agent behaves completely differently. No deployments, no code reviews, no PRs."*

**Storage:**
- *"Seven entities might seem like a lot for a chat app, but each one earns its place: sessions, messages, summaries, tool calls, jobs, job runs, and provider settings."*
- *"Soft deletes via timestamps — we never permanently delete a session, we mark it as deleted. The history is always recoverable."*

---

### Stage 3: Gateway + HTTP NDJSON + Blazor (min 40–52)

- *"The Gateway is NOT a stateless proxy. It's a persistent, stateful control plane. It maintains channel connections, manages cron jobs, handles webhooks, and serves both the management UI and the chat UI. Everything passes through here."*
- *"Minimal APIs aren't just shorter than controllers — they're measurably faster. No model binding overhead, no action filter pipeline unless you explicitly add it."*
- *"HTTP NDJSON streaming replaces WebSocket with simpler HTTP. The `/api/chat/stream` endpoint returns NDJSON — each JSON line is a discrete event the client parses incrementally. Errors surface as HTTP status codes, not protocol-level messages. Debugging is simpler because everything flows over standard HTTP."*
- *"This AppHost replaces what would typically be a Docker Compose file, an environment variable setup script, a startup checklist, and a health check config. It's 18 lines of C#. One command, everything runs."*
- *[During Aspire Dashboard demo]* *"This is your production observability for free. Distributed traces, structured logs, health checks, metrics — all from that one `AddServiceDefaults()` call in Program.cs."*

---

### Closing (min 52–56)

**End-to-end flow recap — say this slowly:**
> *"Let's trace the complete path one last time. You type a message in the Blazor UI. It sends an HTTP POST to the Gateway's `/api/chat/stream` endpoint. The endpoint calls the agent orchestrator. The orchestrator loads the workspace bootstrap files, composes the system prompt, and calls IAgentProvider.CreateChatClient to get an IChatClient. The OllamaAgentProvider sends an HTTP POST to the local LLM and starts reading the NDJSON stream line by line. Each token is yielded back through IAsyncEnumerable, serialized as a JSON line, and flushed to the browser over the HTTP response. The conversation is stored in SQLite. And Aspire is watching all of it."*

**Session 2 preview:**
> *"Everything today is the foundation. The interface, the streaming, the storage, the orchestration — these are the wires. Session 2: the agent gets superpowers. We'll add tools — file system access, web fetching, shell execution — and build the agent loop that decides when and how to use them. When we're done with Session 2, this chat app will be able to read files, search the web, and run commands. All from natural language."*

---

## Presenter Coordination Notes

### Pablo's Responsibilities

- **Before session:** Post repo link + prerequisites checklist in chat 5 minutes before start
- **During Opening:** Run the audience poll; post results in chat
- **Checkpoint 1 (min 17):** Open and arrange VS Code tabs for Bruno before Copilot Moment #1
- **Checkpoint 3 (min 27):** Run `ollama list` and `curl` commands in terminal while Bruno narrates
- **Checkpoint 4 (min 30):** Open `IWorkspaceLoader.cs` for Copilot Moment #2
- **Checkpoint 6 (min 48):** Drive the full-stack demo — run `aspire run`, navigate dashboard, open Blazor UI, type and send the demo message, open DevTools
- **Resources (min 55):** Post all links in chat (repo, Aspire docs, Ollama, Foundry Local, Copilot docs)
- **Q&A (min 56):** Monitor chat, queue questions for Bruno, relay audience questions verbally; answer non-technical questions independently

### Timing Signals

Use a shared timer visible to both presenters. Agreed signals:
- Pablo holds up 1 finger: **1 minute over**, wrap up the current point
- Pablo holds up 2 fingers: **2 minutes over**, move to next section immediately
- Pablo thumbs up: **on time**

### If Running Over

Skip demos in this order (each saves ~6–10 minutes):
1. **Skip Demo 3 (Personality Swap)** (~6 min saved) — mention workspace files conceptually but don't do the live edit. Saves the most time with minimal loss.
2. **Shorten Demo 1 (Provider Switch)** (~4 min saved) — skip the Aspire traces inspection, focus on UI settings change + message response.
3. **Skip Demo 2 (Copilot CLI Error Fix)** (~10 min saved) — this is the most time-consuming. Show it as a slide/recording instead.
4. **Skip DevTools Network inspection from Checkpoint 6** (~2 min saved) — focus on the streaming demo and Aspire Dashboard overview only.

**Target compressions:**
- **50 min session:** Skip Demo 2 + Demo 3 → ~52 min (keep Demo 1 + core content)
- **60 min session:** Skip Demo 2, keep Demo 3 → ~63 min
- **70 min session:** Keep all three demos with shorter timings

### If Running Under

The session now includes all three live demos. If you have extra time:
1. **Extend Q&A** — allocate more time for audience questions
2. **Deep-dive on Aspire Dashboard** (bonus) — show more traces, demonstrate distributed tracing in detail
3. **Extended storage walkthrough** — walk through the 7 entity relationships on the whiteboard
4. **Show bonus code patterns** — demonstrate more of the Agent Framework integrations or skills system
