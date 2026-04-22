# Session 1: Bonus Live Demos

> ⏱️ Use these if you finish the main content early or during Q&A. Each is **3-5 minutes** and self-contained.

---

## Demo A: Switch from Ollama to Foundry Local (Config File Approach)

**Goal:** Show that `IAgentProvider` abstraction lets you swap providers with zero code changes using configuration files.

> **Note:** This demo uses a config-file approach to provider switching. **Demo 1** from `demo-scripts.md` demonstrates the same capability using the runtime Model Providers page — no config file edits needed. This is the "code-based" alternative for those who prefer configuration management or automated deployments.

### Steps

1. **Stop the running app** (Ctrl+C in the AppHost terminal)

2. **Open `appsettings.json`** in `src/OpenClawNet.Gateway/`:
   ```json
   "Model": {
     "Provider": "ollama",
     "Model": "gemma4:e2b"
   }
   ```

3. **Change the provider:**
   ```json
   "Model": {
     "Provider": "foundry-local",
     "Model": "phi-4"
   }
   ```

4. **Restart the app:**
   ```bash
   aspire run
   ```

5. **Open the Blazor UI**, send the same message as before — streaming works identically.

6. **Key talking point:** *"One config change. No code touched. That's the power of the `IAgentProvider` abstraction — the Strategy pattern in action."*

### Fallback
If Foundry Local isn't installed, show the `Program.cs` switch statement and explain how the DI registration changes. Then switch back to Ollama.

---

## Demo B: Aspire Dashboard Deep Dive

**Goal:** Show attendees what they get "for free" with Aspire — distributed tracing, structured logs, health checks.

### Steps

1. **Open the Aspire Dashboard** (`https://localhost:15100` or the URL shown at startup)

2. **Show the Resources tab:**
   - Point out Gateway and Web services, their status (Running), endpoints
   - *"Two lines of code in AppHost gave us service discovery, health monitoring, and this dashboard."*

3. **Send a chat message** in the Blazor UI, then switch back to the dashboard

4. **Show the Traces tab:**
   - Find the trace for your chat request
   - Expand it — show the span from Web → Gateway → Ollama/Foundry Local
   - *"Every HTTP call, every streaming request — automatically traced. No instrumentation code needed."*

5. **Show the Structured Logs tab:**
   - Filter by `OpenClawNet.Gateway`
   - Find the log entry: `Sending chat request to Ollama: model=llama3.2`
   - *"Structured logging with scopes — in production, this goes to App Insights with one line."*

6. **Show the Metrics tab** (if available):
   - Request duration, active connections
   - *"This is production-grade observability from day one."*

### Key talking point
*"Aspire isn't just an orchestrator — it's your local production environment. Everything you see here works the same way in Azure."*

---

## Demo C: Test the Gateway API with curl

**Goal:** Show that the Gateway is a standard REST API — no UI required. Useful for integrations, testing, and understanding the request/response format.

### Steps

1. **Health check:**
   ```bash
   curl http://localhost:5000/health
   ```
   → `{"status":"healthy","timestamp":"..."}`

2. **API version:**
   ```bash
   curl http://localhost:5000/api/version
   ```
   → `{"version":"0.1.0","name":"OpenClawNet"}`

3. **Create a new session:**
   ```bash
   curl -X POST http://localhost:5000/api/sessions \
     -H "Content-Type: application/json" \
     -d '{"title":"API Test"}'
   ```
   → Returns session ID (copy it)

4. **Send a chat message** (non-streaming):
   ```bash
   curl -X POST http://localhost:5000/api/chat \
     -H "Content-Type: application/json" \
     -d '{"sessionId":"<ID>","message":"What is .NET 10?"}'
   ```
   → Returns the full response as JSON

5. **Point out the response structure:**
   - `content`, `toolCallCount`, `totalTokens`
   - *"This is the exact same `ChatMessageResponse` record we walked through in the code. The API is just a thin layer over our abstractions."*

### Key talking point
*"Your Blazor UI is one client. But this API can power a mobile app, a CLI tool, a VS Code extension — anything that speaks HTTP."*

---

## Demo D: Live HTTP NDJSON Stream Inspection

**Goal:** Demystify the real-time streaming — show actual NDJSON lines flowing from the server to the browser.

### Steps

1. **Open the Blazor UI** in Chrome/Edge

2. **Open DevTools** (F12) → **Network** tab → filter by **Fetch/XHR**

3. **Send a chat message** in the UI

4. **Find the `POST /api/chat/stream` request** — click on it

5. **Go to the Response tab** — this shows the NDJSON lines as they arrive:
   - Each line: a complete JSON object like `{"type":"content","content":"Hello",...}`
   - Rapid lines: individual token deltas from the LLM
   - Final line: `{"type":"complete","content":"...","sessionId":"..."}` — signals end of response

6. **Point out the token-by-token flow:**
   - *"Each JSON line is one token delta streamed through the `StreamChat` endpoint. The Gateway reads from the LLM stream and writes each token as a JSON line to the HTTP response."*
   - *"This is why the response appears word-by-word instead of all at once."*

7. **Highlight the simplicity:**
   - Standard HTTP — no WebSocket upgrade, no special protocol
   - *"Errors show up as HTTP status codes. Debugging is trivial — you can even test with curl."*

### Key talking point
*"This is the same streaming pattern used by modern AI APIs. Server-sent tokens over a standard HTTP connection. Each line is self-contained JSON — parse it, render it, done."*

---

## Suggested Order

If you have **5 extra minutes:** Do Demo A (Provider Switch) — it's the strongest architectural point.

If you have **10 extra minutes:** Do Demo A + Demo B (Aspire Dashboard).

If you have **15+ extra minutes:** Do all four in order A → B → C → D.
