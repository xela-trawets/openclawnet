# Session 1: Demo Scripts

Three detailed, step-by-step demonstrations showing the power of OpenClawNet's architecture.

> **💡 Before Demo 1:** Show the "Architecture at a Glance" slide — the compact 3-layer diagram from the session README. This gives the audience a mental model before they see the live app.

---

## Demo 1: "Switch from Local LLM to Azure OpenAI — No Code"

**Duration:** ~8 minutes  
**Prerequisites:** Aspire stack running with Ollama (`aspire run`), Blazor UI at `http://localhost:5001`, Model Providers page working  
**Teaching point:** Runtime model provider abstraction — swap backends at runtime with zero code changes, zero restarts. Model Provider definitions now drive the actual endpoint used for chat — when you configure a provider in the UI, that exact endpoint and credentials are what the chat flow uses (via `ProviderResolver` → `RuntimeModelSettings` sync).

> **📂 Solution:** Open `OpenClawNet.slnx` from the repo root  
> **🚀 Run:** `aspire run` from `C:\src\openclawnet-plan`  
> **📊 Dashboard:** `https://localhost:15100`  
> **🌐 Web UI:** `http://localhost:5001`

### Setup (before session)

- [ ] Verify Ollama is running and `llama3.2` (or preferred model) is available
- [ ] Start the full stack: `aspire run` from repo root
- [ ] Verify Aspire Dashboard is healthy: `https://localhost:15100` (both Gateway and Web showing ✅ Running)
- [ ] Test the Blazor UI: open `http://localhost:5001`, send a test message, verify Ollama responds
- [ ] Have an Azure OpenAI account ready (endpoint URL + API key) OR plan to use "integrated" auth with DefaultAzureCredential if you have Azure CLI logged in
- [ ] Open Model Providers page at `http://localhost:5001/model-providers` and confirm it loads without errors

### Live Steps

#### Step 1: Show current state (Ollama is the provider)
1. **[Action]** Navigate to `http://localhost:5001` in browser
2. **[Action]** Click **New Chat** (or open an existing chat session)
3. **[Show]** **Point out the agent selector badge** in the chat area showing the active provider (Ollama) and model name
4. **[Say]** *"See the agent selector badge in the chat — it shows us the active provider (Ollama, locally) and the model name. This updates when you switch providers."*
5. **[Action]** Type a question: `"What is Aspire and why would I use it?"` and send
6. **[Show]** Watch tokens stream in real-time from Ollama.
7. **[Say]** *"Every word appearing is a token from the local LLM. The agent selector badge shows us the Ollama provider. Fast, low-latency, and completely under our control."*

#### Step 2: Navigate to Model Providers
1. **[Action]** Click the **Model Providers** link in the Settings section of the navigation menu
2. **[Show]** The Model Providers page loads with a list of configured provider definitions
3. **[Say]** *"This is the Model Providers page. It shows all configured provider definitions — you can have multiple named configurations per provider type. You'll see entries for Ollama, Azure OpenAI, Microsoft Foundry, Foundry Local, and GitHub Copilot providers."*

#### Step 3: Add or edit an Azure OpenAI provider definition
1. **[Action]** Click to add a new provider definition or edit an existing one
2. **[Say]** *"Let's configure an Azure OpenAI provider without stopping the app or changing a line of code."*
3. **[Action]** Select **"Azure OpenAI"** as the provider type
4. **[Show]** The form immediately reorganizes:
   - "Endpoint" field appears (for Azure resource URL)
   - "Deployment Name" field appears
   - "Authentication Mode" dropdown appears (API Key vs. Integrated)
   - Model field disappears (Azure OpenAI uses deployment names, not model names)
5. **[Say]** *"See how the UI adapts? That's Blazor responding to the provider change. The form is now configured for Azure."*

#### Step 4: Enter Azure OpenAI credentials
1. **[Action]** Fill in the form:
   - **Endpoint:** Paste your Azure OpenAI resource URL (e.g., `https://my-resource.openai.azure.com/`)
   - **Deployment Name:** Enter the deployment name (e.g., `gpt-4-turbo` or `gpt-5-mini`)
   - **Authentication Mode:** Choose either:
     - **API Key**: Paste your Azure OpenAI API key in the password field
     - **Integrated**: Leave API Key blank (uses DefaultAzureCredential, your current Azure CLI or Visual Studio login)
2. **[Show]** Fields fill in with real values
3. **[Say]** *"These settings are saved to the Model Provider definition in the database. The `ProviderResolver` syncs this to `RuntimeModelSettings`, so the chat flow uses this exact endpoint and credentials — no mismatch between what you test and what you chat with."*

#### Step 5: Save settings
1. **[Action]** Click **"Save Settings"** button
2. **[Show]** Brief loading spinner, then green badge: **✔ Saved**
3. **[Show]** The agent selector badge in the chat will reflect the new provider and model
4. **[Say]** *"Done. The provider definition is now saved. The `ProviderResolver` maps this definition to the runtime, so `RuntimeAgentProvider` will use Azure OpenAI with this exact endpoint for all future chat requests. The agent selector badge will update to reflect the active provider."*

#### Step 6: Send a chat message (now from Azure OpenAI)
1. **[Action]** Navigate back to the chat page (or click **New Chat**)
2. **[Say]** *"Now let's send a message. The same question, but this time it will be answered by Azure OpenAI running in the cloud, not Ollama on this machine."*
3. **[Action]** Type: `"What is Aspire and why would I use it?"` and send
4. **[Show]** Tokens stream in, but from a different source (potentially faster, higher quality — Azure's GPT-4 vs. local Ollama)
5. **[Say]** *"Notice the response style might be different — that's Azure OpenAI's model. Same interface, different backend."*

#### Step 7: Show Aspire Dashboard traces (optional, for deeper teaching)
1. **[Action]** Open **Aspire Dashboard** in another tab: `https://localhost:15100`
2. **[Action]** Go to the **Traces** section
3. **[Show]** Find the recent `/api/chat` call
4. **[Action]** Expand the trace and look for the HTTP call to the model provider
   - **Old trace (Ollama):** Request to `http://localhost:11434/api/generate`
   - **New trace (Azure):** Request to `https://{your-resource}.openai.azure.com/deployments/{deployment}/chat/completions`
5. **[Say]** *"The trace shows us exactly which provider the request went to. Same code, different endpoint. That's the power of clean abstraction."*

#### Step 8: (Bonus) Switch back to Ollama
1. **[Action]** Return to Model Providers page
2. **[Action]** Change provider back to **"Ollama (Local)"**
3. **[Action]** Click **Save Settings**
4. **[Action]** Send another message to verify Ollama is responding again
5. **[Say]** *"This is the beauty of the architecture. You could do this in production — A/B test models, fail over from one provider to another, or migrate to a new LLM provider without touching code."*

### Recovery

- **If Azure credentials fail (401 Unauthorized):** 
  - Verify the endpoint URL is correct (should end with `/`)
  - Verify the API key is correct
  - Check if the deployment exists in the Azure resource
  - Fall back to Integrated auth and verify Azure CLI is logged in (`az account show`)

- **If Model Providers page won't load:**
  - Check Gateway is running: `https://localhost:15100` should show Gateway as healthy
  - Refresh the browser
  - Check browser console (F12) for any errors

- **If the chat doesn't respond after switching:**
  - Verify credentials were saved (Model Providers page should show the configuration)
  - Try sending a message again — there may be a brief delay
  - Check Aspire traces for errors in the HTTP call to the provider

### Transition

> *"That's the first teaching point: clean abstraction makes runtime flexibility possible. The `IAgentProvider` interface is the contract. The `RuntimeAgentProvider` service manages the swap. And the Gateway orchestrates it all — zero restarts, zero downtime. Now let's look at something equally powerful: how to debug when something goes wrong."*

---

## Demo 2: "Copilot CLI + Aspire — Error Detection & Fix"

**Duration:** ~10 minutes  
**Prerequisites:** Aspire stack running, a broken feature or intentional error in the app, GitHub Copilot CLI installed and authenticated  
**Teaching point:** AI-assisted debugging with real observability data. The Aspire dashboard provides the error signal; Copilot CLI reads it and suggests a fix.

> **📂 Solution:** Open `OpenClawNet.slnx` from the repo root  
> **🚀 Run:** `aspire run` from `C:\src\openclawnet-plan`  
> **📊 Dashboard:** `https://localhost:15100`  
> **🌐 Web UI:** `http://localhost:5001`

### Setup (before session)

- [ ] Start Aspire: `aspire run` from repo root
- [ ] Verify Aspire Dashboard is healthy: `https://localhost:15100`
- [ ] Install GitHub Copilot CLI: `npm install -g @github/cli-copilot` (or use existing installation)
- [ ] Verify Copilot CLI is authenticated: `gh auth status` should show you're logged in
- [ ] **Create an intentional error** in the app (choose ONE):
  - **Option A (easiest):** Create a broken Razor component:
    - Create `src/OpenClawNet.Web/Components/Pages/Broken.razor`
    - Add invalid Razor syntax or a missing dependency
    - Add a link to it in the nav so it's clickable during the demo
  - **Option B:** Remove a service from DI in Gateway `Program.cs` so a required dependency fails
  - **Option C:** Call a non-existent API endpoint from a page component
- [ ] Have the broken feature ready to trigger during the demo

### Live Steps

#### Step 1: App is running, navigate to the broken feature
1. **[Say]** *"The app is running in Aspire. We're going to deliberately navigate to a feature that isn't implemented yet — or has a bug."*
2. **[Action]** Open Blazor UI: `http://localhost:5001`
3. **[Action]** Click on the broken feature link (or navigate directly if you know the URL)
4. **[Show]** Page fails to load or throws an error (browser shows error, or network request fails)
5. **[Say]** *"Error. The page didn't load. Instead of hunting through logs manually, we're going to let Copilot CLI read the error from Aspire's traces."*

#### Step 2: Open Copilot CLI terminal side-by-side with Aspire Dashboard
1. **[Action]** Arrange your screen:
   - Left side: Aspire Dashboard (`https://localhost:15100`) showing Traces
   - Right side: Terminal with Copilot CLI ready
2. **[Say]** *"Copilot CLI can read structured data from Aspire — traces, logs, metrics. It understands the OpenTelemetry format. Let's ask it to check for errors."*

#### Step 3: Ask Copilot CLI to analyze Aspire traces
1. **[Action]** In the terminal, run:
   ```bash
   gh copilot explain "Check the Aspire dashboard at https://localhost:15100 for errors in recent traces and describe what went wrong"
   ```
   (Or use the Copilot CLI skill if integrated with the Aspire dashboard)
2. **[Show]** Copilot CLI queries the dashboard, reads recent traces, identifies the error
3. **[Say]** *"Copilot CLI just read the Aspire traces and identified the problem. It's not a guess — it's based on real observability data."*

#### Step 4: Ask for a fix
1. **[Action]** Follow up:
   ```bash
   gh copilot explain "Based on that error, what's the root cause and what code needs to be changed to fix it?"
   ```
2. **[Show]** Copilot CLI explains the error in context (e.g., "Missing service registration," "Null reference exception," etc.)
3. **[Say]** *"Instead of me jumping into the code, Copilot CLI diagnosed the issue from observability data. This is AI-assisted debugging."*

#### Step 5: Copilot CLI proposes and makes the fix
1. **[Action]** Ask:
   ```bash
   gh copilot suggest "Generate the code change to fix this error"
   ```
2. **[Show]** Copilot CLI outputs the exact file path and code change needed
3. **[Action]** Copilot CLI (or you) applies the fix to the code
4. **[Show]** The file is updated (or you manually apply the suggested change)
5. **[Say]** *"One command. Copilot CLI read the error, understood the context, and suggested the exact fix."*

#### Step 6: Verify the fix (Aspire hot reload or restart)
1. **[Action]** Save the file
2. **[Say]** *"If hot reload is enabled, Aspire will recompile automatically. Otherwise, we'll do a quick restart."*
3. **[Action]** If Aspire hot reload doesn't trigger:
   - Stop `aspire run` (Ctrl+C)
   - Run `aspire run` again
4. **[Show]** Aspire services restart and come back healthy
5. **[Show]** Aspire Dashboard shows green health checks again
6. **[Action]** Navigate to the previously broken feature again
7. **[Show]** Page now loads correctly — the error is fixed
8. **[Say]** *"The feature works. What started as an error in Aspire traces became a code fix in minutes — with Copilot CLI reading the telemetry and proposing the solution."*

### Recovery

- **If Copilot CLI can't reach Aspire Dashboard:**
  - Verify Aspire is running: `https://localhost:15100` should load
  - Verify Copilot CLI is authenticated: `gh auth status`
  - Manually open Aspire Dashboard and walk through the error analysis while narrating

- **If the fix doesn't work:**
  - Have a pre-written fix ready as fallback
  - Explain the error conceptually and move to the next demo

- **If Aspire fails to restart after the fix:**
  - Have a pre-built version of the app without the bug ready
  - Or skip the fix verification and focus on the diagnostic part of the demo

### Transition

> *"That demonstrates how observability + AI = faster debugging. But observability is just one side of the coin. The other side is configuration. A lot of agent behavior is driven by data, not code. Let's look at the most powerful example: workspace files."*

---

## Demo 3: "Agent Personality Swap — Workspace Files & Agent Profiles"

**Duration:** ~6 minutes  
**Prerequisites:** Aspire stack running, Blazor UI working, access to the workspace directory on disk (or have pre-edited workspace files ready)  
**Teaching point:** No-code agent customization. Workspace files (`AGENTS.md`, `SOUL.md`, `USER.md`) control agent behavior without touching C# or restarting services. Alternatively, the new **Agent Profiles** page (`/agent-profiles`) lets you create and manage named agent configurations through the UI.

> **📂 Solution:** Open `OpenClawNet.slnx` from the repo root  
> **🚀 Run:** `aspire run` from `C:\src\openclawnet-plan`  
> **📊 Dashboard:** `https://localhost:15100`  
> **🌐 Web UI:** `http://localhost:5001`  
> **📝 Workspace Path:** `src/OpenClawNet.Agent/workspace/`

### Setup (before session)

- [ ] Aspire stack running with `aspire run`
- [ ] Blazor UI working at `http://localhost:5001`
- [ ] Workspace files accessible at `src/OpenClawNet.Agent/workspace/`:
  - `AGENTS.md` — current default persona (professional assistant)
  - `SOUL.md` — core values and guardrails
  - `USER.md` — user profile and preferences
- [ ] Have VS Code (or any text editor) open and ready to edit workspace files
- [ ] Pre-made alternative personas ready (copy these into the workspace during the demo):
  - `docs/sessions/session-1/demo-agents/pirate-agents.md` — Pirate persona
  - `docs/sessions/session-1/demo-agents/chef-agents.md` — Cooking enthusiast persona
  - `docs/sessions/session-1/demo-agents/robot-agents.md` — Quirky robot persona
- [ ] Know how to refresh/restart a chat session (or use Ctrl+F5 to force refresh)

### Live Steps

#### Step 1: Send a message with default personality
1. **[Say]** *"The app is running with a default agent personality. Let's see how it responds."*
2. **[Action]** Open Blazor UI: `http://localhost:5001`
3. **[Action]** Start a **New Chat** session (important: new session, so it loads fresh workspace files)
4. **[Action]** Send a message: `"Hello! What's the best way to debug a .NET app?"` or any technical question
5. **[Show]** The response comes back in the default, professional tone
6. **[Say]** *"That's the default personality — professional, helpful, straightforward. Now let's change it without touching a line of C#."*

#### Step 2: Open the workspace directory and explain the three files
1. **[Say]** *"The agent's behavior is controlled by three optional markdown files in the workspace directory:*
   - *`AGENTS.md` — describes the agent's persona and behavior*
   - *`SOUL.md` — core values and guardrails*
   - *`USER.md` — user-specific preferences*"*
2. **[Say]** *"These files are loaded at the start of every chat session. Change the markdown, start a new chat, and the agent behaves completely differently. No code, no deployment, no restart."*
3. **[Action]** Open `src/OpenClawNet.Agent/workspace/` in VS Code or your editor
4. **[Show]** All three files:
   - `AGENTS.md` — current professional persona
   - `SOUL.md` — values like Honesty, Safety, Privacy, Transparency, Respect for Autonomy, Local-First
   - `USER.md` — user profile (name: "User", preferences: concise and direct)

#### Step 3: Replace AGENTS.md with a fun persona
1. **[Action]** Open `src/OpenClawNet.Agent/workspace/AGENTS.md` in the editor
2. **[Show]** The current content:
   ```
   # Agent Persona
   
   You are **OpenClaw .NET**, a capable and thoughtful AI assistant built on .NET 10.
   
   ## Core Behavior
   - Be helpful and accurate...
   - Be concise...
   - Use tools proactively...
   ```
3. **[Say]** *"This is the agent's personality. Let's replace it with something completely different."*
4. **[Action]** Open `docs/sessions/session-1/demo-agents/pirate-agents.md` (or any of the other persona files)
5. **[Action]** Select all content in `pirate-agents.md` and copy it
6. **[Action]** Switch back to `src/OpenClawNet.Agent/workspace/AGENTS.md`
7. **[Action]** Select all (Ctrl+A) and paste the pirate content
8. **[Say]** *"I've swapped the personality. Look at the difference:*
   ```
   You are **Captain Claw**, a swashbuckling pirate who loves .NET...
   - Speak like a pirate! Use 'Arr', 'Yo ho ho', 'Shiver me timbers'...
   ```"*
9. **[Action]** Save the file (Ctrl+S)
10. **[Say]** *"Saved. The WorkspaceLoader will pick this up on the next session start."*

#### Step 4: Start a new chat session (to load the updated workspace file)
1. **[Action]** Refresh the Blazor UI browser tab (Ctrl+F5) or navigate to a new chat
2. **[Action]** Click **New Chat** to start a fresh session
3. **[Say]** *"New session = new workspace load. The AGENTS.md file we just edited will be injected into the system prompt."*
4. **[Show]** The chat interface resets (new, empty conversation)

#### Step 5: Send the same message (watch the personality transform)
1. **[Action]** Send the same question: `"Hello! What's the best way to debug a .NET app?"`
2. **[Show]** The response comes back in **pirate speak** while still answering the question accurately:
   - *"Arr, what a fine question! Here be the way to debug yer .NET app, ye landlubber..."*
   - Includes actual debugging advice (breakpoints, Aspire traces, DevTools, etc.)
3. **[Say]** *"Same question, completely different personality. Zero code changes. That's the power of workspace files."*

#### Step 6: (Bonus) Show SOUL.md and USER.md
1. **[Say]** *"AGENTS.md changes personality. But SOUL.md and USER.md are equally important."*
2. **[Action]** Open `src/OpenClawNet.Agent/workspace/SOUL.md` in the editor
3. **[Show]** Content example:
   ```
   # Core Values
   
   ## Honesty
   Always tell the truth. If a tool returns an error, report it.
   
   ## Safety
   Decline requests that could harm users or systems...
   ```
4. **[Say]** *"SOUL.md is the agent's conscience. These are the values it uphold, the guardrails that keep it safe. You can edit this to customize the agent's ethical boundaries."*
5. **[Action]** Open `src/OpenClawNet.Agent/workspace/USER.md` in the editor
6. **[Show]** Content example:
   ```
   # User Profile
   
   ## Name
   User
   
   ## Preferences
   - Response style: Concise and direct
   - Code format: Include language tags
   ```
7. **[Say]** *"USER.md personalizes responses. Tell the agent about you — your name, your preferences — and it tailors every response just for you."*

#### Step 7: (Optional) Switch to a different personality again
1. **[Action]** Open `docs/sessions/session-1/demo-agents/chef-agents.md` (or robot-agents.md)
2. **[Action]** Copy the content
3. **[Action]** Paste it into `src/OpenClawNet.Agent/workspace/AGENTS.md` and save
4. **[Action]** In the Blazor UI, click **New Chat** again
5. **[Action]** Send a message and watch the agent respond as a chef or robot
6. **[Say]** *"That's the power of workspace files. You can iterate on agent personality in real time — for testing, for customization, for A/B testing different personas — without any code changes or deployments."*

#### Step 8: (Alternative) Show the Agent Profiles page
1. **[Say]** *"Workspace files are great for development and quick iteration. But for a more structured approach, OpenClawNet also has an Agent Profiles page."*
2. **[Action]** Navigate to `http://localhost:5001/agent-profiles`
3. **[Show]** The Agent Profiles page with a list of saved agent configurations (stored in SQLite as `AgentProfile` entities)
4. **[Say]** *"Agent Profiles are named configurations stored in the database. Each profile includes a persona, model preferences, and behavior settings. You can create, edit, and switch between profiles through the UI — no file editing required."*
5. **[Show]** Click **Create New Profile** (or show an existing one) to illustrate the fields
6. **[Say]** *"Both approaches work: workspace files for quick file-based iteration during development, and Agent Profiles for structured, persistent configurations in the UI. Use whichever fits your workflow."*

### Recovery

- **If the workspace files don't reload:**
  - Verify the file paths are correct (check Aspire logs for workspace directory location)
  - Hard refresh the browser (Ctrl+Shift+R)
  - Restart Aspire
  - Fall back to narrating the concept and showing the file content

- **If you can't find the workspace directory:**
  - Check Aspire Dashboard → Resources → look at the service container mounts
  - Or set up a test workspace directory in the repo root and point to it

- **If the agent response doesn't change:**
  - Verify the file was saved
  - Verify you started a **new** session (not continuing an old conversation)
  - Check that the workspace file contains valid markdown

### Transition

> *"This is the kind of flexibility that production systems need. Your agent's behavior is decoupled from your deployment pipeline. You can iterate on personality and values independently of code. That's what workspace files give you — runtime configuration for the things that matter most: tone, values, and personalization."*

---

## Key Teaching Points (Repeat Across All 3 Demos)

1. **Demo 1 — Abstraction enables runtime flexibility:**
   - `IAgentProvider` is the contract
   - Implementation can swap without code changes
   - This scales: local → cloud, experimental → production, GPU → CPU

2. **Demo 2 — Observability + AI = faster debugging:**
   - Aspire provides structured traces
   - Copilot CLI reads observability data
   - Errors become actionable insights, not guesses

3. **Demo 3 — Configuration as data, not code:**
   - Workspace files are the simplest API for customization
   - Agent Profiles provide a UI-driven alternative stored in SQLite
   - No deployments, no builds, no restarts
   - Anyone (non-engineers) can edit a markdown file or use the Agent Profiles page to change behavior

---

## Demo Checklist (Run 30 min before session)

- [ ] Aspire dashboard loads: `https://localhost:15100` ✅
- [ ] Blazor UI responds: `http://localhost:5001` ✅
- [ ] Send test message (Demo 1 sanity check) ✅
- [ ] Model Providers page works (Demo 1 prerequisite) ✅
- [ ] Azure OpenAI credentials ready OR Azure CLI logged in (Demo 1) ✅
- [ ] Broken feature or error reproducible (Demo 2) ✅
- [ ] Copilot CLI installed and authenticated (Demo 2) ✅
- [ ] Workspace directory accessible (Demo 3) ✅
- [ ] AGENTS.md can be edited and refreshed (Demo 3) ✅
- [ ] Timings are realistic for your setup (adjust if LLM is slow) ✅

---

## Notes for Presenters

### Timing
- **Demo 1:** 8 min (+ 2 min buffer if Azure setup is slow)
- **Demo 2:** 10 min (+ 3 min buffer if Copilot CLI is slow)
- **Demo 3:** 6 min (very reliable, low latency)

### On-Stage Coordination
- **Demo 1:** One person navigates UI, other narrates architectural point
- **Demo 2:** One person watches Aspire Dashboard, other runs Copilot CLI commands
- **Demo 3:** One person edits files, other watches chat response change

### Fallbacks
Each demo has recovery steps. If something goes wrong:
1. Don't panic — have a screenshot or recording as backup
2. Pivot to the teaching point (you can explain it without the live demo)
3. Move to the next demo

---

