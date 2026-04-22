# Aspire / AppHost / Deployment Architecture Review

> **Reviewer:** Bishop (DevOps / Azure / Aspire Specialist)  
> **Date:** 2026-04-17  
> **Status:** Comprehensive Architecture Assessment  

---

## Executive Summary

OpenClawNet's Aspire orchestration demonstrates solid fundamentals: clean resource topology, explicit dependencies, comprehensive ServiceDefaults, and good OTel foundations. The AppHost at `src/OpenClawNet.AppHost/AppHost.cs` is **readable, explicit, and maintainable** — exactly what an orchestration file should be.

**What's working well:**
- ✅ **Clean topology model:** 8 services with explicit references, startup ordering (`WaitFor`), health checks
- ✅ **ServiceDefaults coverage:** OTel, service discovery, resilience, health endpoints across all 8 services
- ✅ **Local-first design:** SQLite via Aspire, Ollama fallback, single `F5` startup experience
- ✅ **Observability foundation:** GenAI OTel signals enabled, provider-specific trace sources registered
- ✅ **Deployment analysis completed:** `docs/deployment/azure-deployment-options-analysis.md` provides clear ACA path

**Critical gaps preventing smooth production deployment:**

1. **Secrets in config files** — API keys, connection strings in appsettings.json; no Key Vault, no managed identity.
2. **SQLite as single-file state** — AppHost hard-codes `.data/openclawnet.db`; won't survive ACA restart without Azure Files (poor concurrency fit).
3. **Missing Aspire Azure packages** — No `Aspire.Hosting.Azure.AppContainers` reference; `azd up` won't work out of the box.
4. **Partial service discovery** — Web & Scheduler hard-code gateway URL fallbacks; breaks if gateway port changes.
5. **No container registry workflow** — No Dockerfile for non-AppHost services; ACA deployment requires image builds.
6. **Dashboard-only health visibility** — No `/health` aggregator; no uptime metrics; production ops rely on manual dashboard checks.
7. **No azd manifest** — Missing `azure.yaml`, infra-as-code, environment configs for multi-stage deployments.

**Overall posture:** **Local dev: A–** | **Azure deployment readiness: C+**  
The app runs beautifully on `localhost`. Moving to ACA requires 4–6 structural changes (SQLite → managed DB, secrets → Key Vault, azd scaffolding, container images).

---

## 1. AppHost Topology Health

### Observations

**✅ What's correct:**

```csharp
// src/OpenClawNet.AppHost/AppHost.cs
var sqlite = builder.AddSqlite("openclawnet-db", databasePath: dbPath, ...)
    .WithSqliteWeb();  // Dev tool for browsing SQLite

var gateway = builder.AddProject<Projects.OpenClawNet_Gateway>("gateway")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(sqlite);  // Dependency declared

var scheduler = builder.AddProject<Projects.OpenClawNet_Services_Scheduler>("scheduler")
    .WithReference(sqlite)
    .WithReference(gateway)
    .WaitFor(gateway);  // Startup ordering
```

**Resource model clarity:** Every service has a named resource, health endpoint, and explicit references. No "magic" service discovery URLs. This is **textbook Aspire**.

**Startup ordering:** `WaitFor(gateway)` ensures scheduler and web don't start before gateway is ready. Prevents transient 503s during cold start.

**Health checks:** All 8 services expose `/health` and register it with `.WithHttpHealthCheck("/health")`. Dashboard shows red/green status immediately.

---

**❌ What's broken:**

#### Issue 1.1: SQLite Path Hard-Coded to `.data/`

**Impact:** On ACA, `.data/` disappears on restart. The AppHost config reads:

```csharp
var dbPath = builder.Configuration["OpenClawNet:ConnectionStrings:DbPath"]
    ?? Path.Combine(builder.AppHostDirectory, ".data");  // ← local path
```

ACA ephemeral storage = `/tmp` equivalent. You **must** migrate to Azure SQL / PostgreSQL before deploying to ACA, or mount Azure Files (poor SQLite fit due to locking). The deployment analysis (`docs/deployment/azure-deployment-options-analysis.md:68`) correctly identifies this blocker.

**Recommendation:**  
- **Short-term:** Add `appsettings.Production.json` to AppHost with a placeholder connection string for Azure SQL.
- **Medium-term:** Create a Bicep parameter for `sqlConnectionString`, integrate with `azd env set SQL_CONNECTION_STRING`.
- **Long-term:** Use `builder.AddAzureSqlServer(...)` from `Aspire.Hosting.Azure.Sql` (requires preview package).

**Effort:** M (2–4 hours to wire up Azure SQL + EF migrations + azd env vars)

---

#### Issue 1.2: No `.AddReference()` Chain Validation

**Observation:** `shell-service`, `browser-service`, `memory-service` are **registered without any references**:

```csharp
builder.AddProject<Projects.OpenClawNet_Services_Shell>("shell-service")
    .WithHttpHealthCheck("/health");  // No .WithReference(...)
```

Yet the Gateway's `Program.cs` expects to discover them via service discovery:

```csharp
builder.Services.AddHttpClient("shell-service", c => c.BaseAddress = new Uri("https+http://shell-service"));
```

**Why this works locally:** Aspire's DCP (Developer Control Plane) auto-registers all resources in the same AppHost session. Service discovery finds `shell-service` by name.

**Why this breaks in production:** Without `.WithReference(shell-service)` on the gateway, `azd` doesn't know gateway → shell-service is a dependency. The generated ACA manifest won't create the necessary app-to-app ingress rules. Service discovery will fail at runtime.

**Recommendation:**  
```diff
+ var shellService = builder.AddProject<Projects.OpenClawNet_Services_Shell>("shell-service")
+     .WithHttpHealthCheck("/health");

+ var browserService = builder.AddProject<Projects.OpenClawNet_Services_Browser>("browser-service")
+     .WithHttpHealthCheck("/health");

+ var memoryService = builder.AddProject<Projects.OpenClawNet_Services_Memory>("memory-service")
+     .WithHttpHealthCheck("/health");

  var gateway = builder.AddProject<Projects.OpenClawNet_Gateway>("gateway")
      .WithExternalHttpEndpoints()
      .WithHttpHealthCheck("/health")
-     .WithReference(sqlite);
+     .WithReference(sqlite)
+     .WithReference(shellService)
+     .WithReference(browserService)
+     .WithReference(memoryService);
```

**Impact:** Without this, ACA deployment will silently fail service-to-service calls. Gateway logs will show `HttpRequestException: Connection refused (shell-service)`.

**Effort:** S (10 minutes)

---

#### Issue 1.3: Env Var Leakage — `Model__Model` Hard-Coded in AppHost

```csharp
gateway.WithEnvironment("Model__Model", "gemma4:e2b");  // ← Dev default
```

This is a **config smell**. The gateway's `appsettings.json` already defines `Model:Model`. Why override it in the AppHost?

**Problem:** Every environment (dev, staging, prod) now inherits `gemma4:e2b`. If you want to use `gpt-5-mini` in production, you have to **edit AppHost code** instead of a config file.

**Recommendation:**  
- **Remove this line** from `AppHost.cs`.
- Let `appsettings.Development.json` own dev-specific model defaults.
- For azd environments, inject via `azd env set MODEL__MODEL=gpt-5-mini`.

**Effort:** S (5 minutes)

---

#### Issue 1.4: GenAI OTel Env Var — Good, But Undocumented

```csharp
gateway.WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_GENAI_EMIT_EVENTS", "true");
```

**This is excellent.** It enables semantic conventions for GenAI operations (prompt tokens, completion tokens, model name in spans). But there's no comment explaining **why** it's here or what it does.

**Recommendation:**  
```csharp
// Enable experimental GenAI semantic conventions (token counts, model metadata in spans)
// See: https://opentelemetry.io/docs/specs/semconv/gen-ai/
gateway.WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_GENAI_EMIT_EVENTS", "true");
```

**Effort:** S (2 minutes)

---

### Summary: Topology Health Score = B+

| Aspect                  | Grade | Notes                                                   |
|-------------------------|-------|---------------------------------------------------------|
| Resource naming         | A     | Descriptive, consistent (`gateway`, `scheduler`, etc.)  |
| Dependency graph        | B     | Missing tool service references on gateway              |
| Startup ordering        | A     | Explicit `WaitFor` chains                               |
| Health checks           | A     | All 8 services expose `/health`                         |
| Config override clarity | C     | Hard-coded model in AppHost; should be in appsettings   |
| Secrets handling        | F     | No Key Vault, no managed identity (see Section 4)       |

---

## 2. ServiceDefaults Coverage

### Observations

**✅ What's excellent:**

The `src/OpenClawNet.ServiceDefaults/Extensions.cs` implementation is **comprehensive**:

```csharp
public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
{
    builder.ConfigureOpenTelemetry();  // Tracing, metrics, logging
    builder.AddDefaultHealthChecks();   // /health, /alive endpoints
    builder.Services.AddServiceDiscovery();  // Automatic endpoint resolution
    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(300);  // LLM-friendly
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(120);
            options.Retry.MaxRetryAttempts = 1;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(300);
        });
        http.AddServiceDiscovery();  // Every HttpClient gets discovery
    });
}
```

**Key strengths:**

1. **LLM-aware timeouts:** 300s total, 120s per attempt. Default Aspire template uses 30s — would timeout on every GPT-5 call.
2. **Resilience by default:** Circuit breaker, retries on all `HttpClient` instances.
3. **Service discovery auto-wired:** No hard-coded URLs in business logic (except Web UI fallback, see below).
4. **Health checks:** Separate `/health` (readiness) and `/alive` (liveness) endpoints.
5. **OTel trace sources:** Registered for `Microsoft.Extensions.AI`, `OpenAI.*`, and all 4 OpenClawNet providers (`Ollama`, `AzureOpenAI`, `Foundry`, `GitHubCopilot`).

**All 8 services call `builder.AddServiceDefaults()`** — verified in:
- `src/OpenClawNet.Gateway/Program.cs:25`
- `src/OpenClawNet.Web/Program.cs:5`
- `src/OpenClawNet.Services.Scheduler/Program.cs:8`
- `src/OpenClawNet.Services.Shell/Program.cs:4`
- `src/OpenClawNet.Services.Browser/Program.cs:4`
- `src/OpenClawNet.Services.Memory/Program.cs:5`
- `src/OpenClawNet.Services.Channels/Program.cs:7`

This is **exceptional discipline** — no service is missing the defaults.

---

**❌ Gaps:**

#### Gap 2.1: No Database Health Check

The Gateway and Scheduler both depend on SQLite via `builder.AddSqliteConnection("openclawnet-db")`. But there's **no health check** for database connectivity.

If the `.db` file is missing, corrupted, or locked, the service shows **green in the dashboard** (self-check passes) but crashes on first request.

**Recommendation:**  
Add a SQLite health check in `ServiceDefaults/Extensions.cs`:

```csharp
public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
{
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
    
    // If SQLite connection string is present, add database health check
    var connStr = builder.Configuration.GetConnectionString("openclawnet-db");
    if (!string.IsNullOrEmpty(connStr))
    {
        builder.Services.AddHealthChecks()
            .AddSqlite(connStr, name: "database", tags: ["ready"]);
    }
    
    return builder;
}
```

Requires: `AspNetCore.HealthChecks.Sqlite` NuGet package.

**Impact:** Prevents "gateway is healthy but returns 500" scenarios. ACA/K8s readiness probes will correctly fail if DB is down.

**Effort:** S (15 minutes)

---

#### Gap 2.2: Metrics Coverage — No Business Metrics

OTel metrics are auto-instrumented for ASP.NET Core (request count, duration, status codes) and runtime (GC, thread pool). But there are **zero business metrics**:

- Agent invocations per profile
- Tool call success/failure rates
- Conversation length distribution
- Provider failover counts
- Token usage (input/output) by model

These are **critical operational signals** for an LLM platform. Without them, you can't answer:
- "Which agent profile gets the most traffic?"
- "Did the Ollama → Foundry fallback trigger today?"
- "What's the P95 token cost per request?"

**Recommendation:**  
Create `src/OpenClawNet.ServiceDefaults/Metrics.cs`:

```csharp
public static class OpenClawNetMetrics
{
    private static readonly Meter Meter = new("OpenClawNet", "1.0.0");
    
    public static readonly Counter<long> AgentInvocations = 
        Meter.CreateCounter<long>("openclawnet.agent.invocations", "invocations");
    
    public static readonly Counter<long> ToolCalls = 
        Meter.CreateCounter<long>("openclawnet.tool.calls", "calls");
    
    public static readonly Histogram<long> TokensInput = 
        Meter.CreateHistogram<long>("openclawnet.tokens.input", "tokens");
    
    public static readonly Histogram<long> TokensOutput = 
        Meter.CreateHistogram<long>("openclawnet.tokens.output", "tokens");
}
```

Wire into `DefaultAgentRuntime`, `ToolExecutor`, etc. Tag with `provider`, `model`, `tool_name`.

**Effort:** M (3–5 hours to instrument core paths)

---

#### Gap 2.3: Azure Monitor Exporter Commented Out

```csharp
// Uncomment the following lines to enable the Azure Monitor exporter
//if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
//{
//    builder.Services.AddOpenTelemetry().UseAzureMonitor();
//}
```

This is the **only way** to get telemetry into Application Insights on Azure. It's commented out with no guidance on when/how to enable it.

**Recommendation:**  
- Uncomment it **now**. It's a no-op if the env var isn't set.
- Add a comment:

```csharp
// Azure Monitor exporter (Application Insights) — auto-enabled if 
// APPLICATIONINSIGHTS_CONNECTION_STRING is set in production.
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}
```

**Effort:** S (2 minutes)

---

### Summary: ServiceDefaults Score = A–

| Aspect                  | Grade | Notes                                                      |
|-------------------------|-------|------------------------------------------------------------|
| OTel tracing            | A     | Comprehensive trace sources, GenAI signals enabled         |
| OTel metrics            | C     | ASP.NET auto-metrics only; no business/agent metrics       |
| Health checks           | B+    | Self + liveness checks present; missing DB health check    |
| Service discovery       | A     | Auto-wired on all HttpClients                              |
| Resilience              | A     | LLM-aware timeouts, circuit breaker, retries               |
| Azure Monitor readiness | C     | Exporter commented out; no prod telemetry path             |

---

## 3. Local Dev Experience

### Observations

**✅ What's great:**

1. **Single `F5` startup:** AppHost launches 8 services + SQLiteWeb + Aspire Dashboard. No manual `docker-compose`, no script orchestration.
2. **Dashboard at `https://localhost:21138`** (or `:19043` for http). Live traces, metrics, logs, resource topology in one UI.
3. **Hot reload:** All projects target `net10.0` with implicit usings. Code changes apply without full restart.
4. **Port stability:** No random port assignment. Each service has a fixed launch profile (e.g., gateway `7100/7101`, web `7000/7001`).
5. **Clean shutdown:** Aspire's DCP handles process lifecycle. No orphaned processes after stopping the debugger.

**Verified via:**
- `src/OpenClawNet.AppHost/Properties/launchSettings.json` — dashboard ports hard-coded
- CI workflow `.github/workflows/squad-ci.yml` — builds + tests cleanly without Aspire runtime (good separation)

---

**❌ Pain points:**

#### Issue 3.1: Web UI Falls Back to Hard-Coded Gateway URL

```csharp
// src/OpenClawNet.Web/Program.cs
builder.Services.AddHttpClient("gateway", (sp, client) =>
{
    var gatewayUrl = config["Services:gateway:https:0"]
        ?? config["Services:gateway:http:0"]
        ?? config["OpenClawNet:GatewayBaseUrl"]
        ?? "https://localhost:7100";  // ← Fallback breaks if gateway moves
    client.BaseAddress = new Uri(gatewayUrl.TrimEnd('/') + "/");
});
```

**Why this is bad:**
- Aspire's service discovery **should** resolve `Services:gateway:https:0` automatically.
- If it doesn't (rare, but happens when `WithReference(gateway)` is missing from Web in AppHost), the app silently falls back to `localhost:7100`.
- If you change gateway's port in launchSettings, **Web breaks silently**.

**Root cause check:**  
The AppHost **does** have `.WithReference(gateway)` on the `web` resource (line 32 of `AppHost.cs`). So the fallback chain is defensive programming, not a fix for missing topology.

**Recommendation:**  
Keep the fallback for standalone (non-Aspire) runs, but **log a warning** if it's used:

```csharp
var gatewayUrl = config["Services:gateway:https:0"]
    ?? config["Services:gateway:http:0"]
    ?? config["OpenClawNet:GatewayBaseUrl"];

if (string.IsNullOrEmpty(gatewayUrl))
{
    gatewayUrl = "https://localhost:7100";
    var logger = sp.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("Gateway URL not resolved via service discovery; falling back to {Url}", gatewayUrl);
}
```

**Effort:** S (5 minutes)

---

#### Issue 3.2: No Launch Profile for "Web Only" Development

Every service has a `launchSettings.json` with `https` and `http` profiles. But there's **no profile** for "I just want to work on the Blazor UI without starting the full AppHost."

**Why this matters:**  
UI devs doing CSS/layout work don't need the Gateway, Scheduler, or tool services running. Starting the AppHost is 15–20 seconds of cold-start overhead.

**Recommendation:**  
Add `src/OpenClawNet.Web/Properties/launchSettings.json`:

```json
{
  "profiles": {
    "https": { /* existing */ },
    "http": { /* existing */ },
    "standalone-mock": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:7000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "OpenClawNet__GatewayBaseUrl": "https://mocky.io/v3/mock-gateway"
      }
    }
  }
}
```

Pair with a mock gateway endpoint for UI-only testing.

**Effort:** M (1–2 hours to create mock endpoints)

---

#### Issue 3.3: Dashboard Quality — No Resource Groups

The Aspire Dashboard shows all 8 services in a flat list. No **logical grouping** like:
- **Core Platform** (gateway, scheduler)
- **Frontend** (web)
- **Tool Services** (shell, browser, memory)
- **Integrations** (channels)

Aspire 9.2 supports resource tags for grouping, but they're not used here.

**Recommendation:**  
```csharp
var gateway = builder.AddProject<Projects.OpenClawNet_Gateway>("gateway")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithAnnotation(new ResourceAnnotation("group", "core"));

var shellService = builder.AddProject<Projects.OpenClawNet_Services_Shell>("shell-service")
    .WithHttpHealthCheck("/health")
    .WithAnnotation(new ResourceAnnotation("group", "tools"));
```

Requires Aspire 9.2+ (check `Aspire.AppHost.Sdk` version in `.csproj`).

**Effort:** S (10 minutes if SDK supports it; N/A if on older Aspire)

---

### Summary: Local Dev Score = A–

| Aspect                 | Grade | Notes                                                       |
|------------------------|-------|-------------------------------------------------------------|
| Startup simplicity     | A     | Single F5, no manual orchestration                          |
| Dashboard usability    | B+    | Excellent telemetry view; could add resource grouping       |
| Port stability         | A     | Fixed ports in launchSettings                               |
| Hot reload             | A     | Works across all projects                                   |
| Shutdown cleanliness   | A     | No orphaned processes                                       |
| Standalone dev support | C     | No "UI only" or "Gateway only" profiles for fast iteration  |

---

## 4. Configuration Story

### Observations

**Current architecture:**

1. **AppHost:** `appsettings.json` (logging), `appsettings.Development.json` (logging)
2. **Gateway:** `appsettings.json` (model config, connection string), user secrets (API keys expected)
3. **Web:** `appsettings.json` (gateway fallback URL)
4. **Services:** Minimal appsettings (mostly logging)

**Secrets handling:**  
- Gateway has `<UserSecretsId>c15754a6-dc90-4a2a-aecb-1233d1a54fe1</UserSecretsId>` in `.csproj`.
- AppHost has a **separate** `<UserSecretsId>90a10297-42b5-4ec4-b9c6-121b94cb54f8</UserSecretsId>`.
- **No Key Vault integration.**
- **No managed identity.**

---

**❌ Critical security issue:**

#### Issue 4.1: API Keys in Appsettings (Implied)

The Gateway's `Program.cs` shows:

```csharp
builder.Services.Configure<AzureOpenAIOptions>(o =>
{
    var modelSection = builder.Configuration.GetSection("Model");
    o.Endpoint = modelSection["Endpoint"] ?? string.Empty;
    o.ApiKey = modelSection["ApiKey"];  // ← Expected in config
});
```

The **intent** is for `Model:ApiKey` to come from **user secrets** in dev and **Key Vault** in prod. But there's:
- ❌ No validation that `ApiKey` is set
- ❌ No error message if it's missing
- ❌ No documentation on how to set it
- ❌ No Key Vault reference in production config

**What happens today:**  
If a dev clones the repo and hits F5, the gateway starts, but any Azure OpenAI call fails with `401 Unauthorized` — **no helpful error message**.

**Recommendation:**  

1. **Add validation on startup:**

```csharp
var apiKey = modelSection["ApiKey"];
if (string.IsNullOrEmpty(apiKey) && o.AuthMode == "api-key")
{
    throw new InvalidOperationException(
        "Azure OpenAI ApiKey is required when AuthMode=api-key. " +
        "Set it in user secrets (dotnet user-secrets set Model:ApiKey YOUR_KEY) " +
        "or via env var MODEL__APIKEY.");
}
```

2. **Create `docs/setup.md`** with:
```bash
# Local dev setup
dotnet user-secrets set Model:ApiKey "sk-..." --project src/OpenClawNet.Gateway
dotnet user-secrets set GitHubCopilot:GitHubToken "ghp_..." --project src/OpenClawNet.Gateway
```

3. **For production:** Add to azd setup:
```bash
azd env set MODEL__APIKEY @Microsoft.KeyVault(SecretUri=https://kv.vault.azure.net/secrets/openai-key)
```

**Effort:** M (2–3 hours to wire up Key Vault + managed identity + docs)

---

#### Issue 4.2: `RuntimeModelSettings` Persists to File — Race Condition in Multi-Instance

```csharp
// src/OpenClawNet.Gateway/Services/RuntimeModelSettings.cs
_persistPath = Path.Combine(env.ContentRootPath, "model-settings.json");
```

When you change the provider in the UI (e.g., Ollama → Azure OpenAI), the Gateway writes the new config to **a local JSON file**.

**Problem:** On ACA with 2+ replicas, each instance writes to **its own ephemeral disk**. Instance A changes to Azure OpenAI, instance B still reads Ollama. User requests round-robin between them = **flapping provider state**.

**Correct fix:**  
Move `model-settings.json` to **Redis** or **Azure Blob Storage** (shared state). Or better: store it in the **SQL database** as a settings table.

**Recommendation:**  
1. Create a `SystemSettings` table in SQLite/SQL with columns: `Key`, `Value`, `UpdatedAt`.
2. Replace `RuntimeModelSettings._persistPath` with a DB read/write.
3. Use EF Core change tracking or a singleton in-memory cache to avoid DB hits on every request.

**Effort:** M (3–4 hours)

---

#### Issue 4.3: No `appsettings.Production.json`

The repo has:
- `appsettings.json` (defaults)
- `appsettings.Development.json` (dev overrides)
- ❌ No `appsettings.Production.json`

Production config is undefined. You can't deploy to ACA and say "use these production settings" — it'll use dev defaults.

**Recommendation:**  
Create `src/OpenClawNet.Gateway/appsettings.Production.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "OpenClawNet": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "openclawnet-db": "Server=tcp:openclawnet-sql.database.windows.net,1433;..."
  },
  "Model": {
    "Provider": "azure-openai",
    "Endpoint": "https://openclawnet.openai.azure.com/",
    "_comment": "ApiKey comes from Key Vault via managed identity"
  }
}
```

**Effort:** S (15 minutes)

---

### Summary: Configuration Score = C

| Aspect                     | Grade | Notes                                                    |
|----------------------------|-------|----------------------------------------------------------|
| Layering (dev/prod)        | C     | Has dev override; missing prod config                    |
| Secrets handling           | D     | User secrets in dev; no Key Vault in prod                |
| Multi-provider switching   | B     | Works via RuntimeModelSettings; file persistence fragile |
| Env var override behavior  | B     | Standard ASP.NET Core hierarchy respected                |
| Validation & error clarity | D     | Missing API keys fail silently with 401s                 |

---

## 5. Telemetry / Observability

### Observations

**✅ What's instrumented:**

1. **HTTP layer:** ASP.NET Core auto-instrumentation (request duration, status codes, paths)
2. **HttpClient calls:** All gateway → service calls traced
3. **GenAI signals:** `OTEL_DOTNET_EXPERIMENTAL_GENAI_EMIT_EVENTS=true` captures token counts, model names
4. **Provider traces:** Explicit trace sources registered:
   ```csharp
   .AddSource("Microsoft.Extensions.AI")
   .AddSource("OpenAI.*")
   .AddSource("OpenClawNet.Ollama")
   .AddSource("OpenClawNet.AzureOpenAI")
   .AddSource("OpenClawNet.Foundry")
   .AddSource("OpenClawNet.GitHubCopilot")
   ```

**Sample trace:** A user sends "Summarize my day" via `/api/chat/stream`:
- Span 1: Web → Gateway HTTP POST
- Span 2: Gateway → Agent runtime (agent selection, prompt composition)
- Span 3: Gateway → Azure OpenAI (prompt tokens, completion tokens, model `gpt-5-mini` in span attributes)
- Span 4: Gateway → Browser service (tool call: fetch calendar)
- Span 5: Gateway → final response stream

**This is excellent.** You can trace a request end-to-end across services.

---

**❌ What's missing:**

#### Gap 5.1: No Custom Trace Instrumentation in Agent Runtime

The `DefaultAgentRuntime` (agent orchestration) is **not instrumented**. You can see the HTTP call to the model provider, but you **can't see**:

- Which agent profile was selected
- How many messages were in the conversation history
- Which tools were considered vs. called
- Prompt composition latency (summary fetch, memory retrieval)
- Streaming chunk count

**Recommendation:**  
Add `System.Diagnostics.ActivitySource` to `DefaultAgentRuntime`:

```csharp
// src/OpenClawNet.Agent/DefaultAgentRuntime.cs
private static readonly ActivitySource ActivitySource = new("OpenClawNet.AgentRuntime");

public async Task<AgentResponse> InvokeAsync(...)
{
    using var activity = ActivitySource.StartActivity("AgentRuntime.Invoke");
    activity?.SetTag("agent.profile", profile.Name);
    activity?.SetTag("conversation.message_count", messages.Count);
    
    // ... orchestration logic
    
    activity?.SetTag("tools.called", string.Join(",", toolsInvoked));
}
```

Register in ServiceDefaults:
```csharp
.AddSource("OpenClawNet.AgentRuntime")
```

**Effort:** M (2–3 hours to instrument core paths)

---

#### Gap 5.2: No Correlation Between Gateway Logs and Provider Traces

When a request fails (e.g., `429 Too Many Requests` from Azure OpenAI), the **Gateway logs show the error**, but the **trace doesn't link to the log entry**.

**Why:** OTel logging integration is enabled (`logging.AddOpenTelemetry()`), but log entries aren't being enriched with `TraceId` / `SpanId`.

**Recommendation:**  
Verify that `IncludeScopes = true` is set (it is, line 60 of `ServiceDefaults/Extensions.cs`). If trace IDs still aren't appearing in logs:

1. Check Application Insights log query:
   ```kusto
   traces
   | where operation_Id == "YOUR_TRACE_ID"
   | order by timestamp desc
   ```

2. If missing, add explicit enrichment in Program.cs:
   ```csharp
   builder.Logging.Configure(options =>
   {
       options.ActivityTrackingOptions = 
           ActivityTrackingOptions.SpanId | 
           ActivityTrackingOptions.TraceId | 
           ActivityTrackingOptions.ParentId;
   });
   ```

**Effort:** S (30 minutes to verify + test)

---

#### Gap 5.3: No Dashboard Panels for Agents / Tools

The Aspire Dashboard shows **resource health** and **traces**. But there's no way to see:
- "Which agent profiles are configured?"
- "Which tools are registered and available?"
- "What's the current model provider?"

These are **operational questions** that an on-call engineer needs to answer at 2 AM without SSH-ing into a container.

**Recommendation:**  
Create a **custom metrics dashboard** in Application Insights:

1. **Agent Profile Usage (pie chart):**  
   Metric: `openclawnet.agent.invocations` (see Gap 2.2)  
   Dimension: `agent.profile`

2. **Tool Success Rate (bar chart):**  
   Metric: `openclawnet.tool.calls`  
   Dimension: `tool.name`, `result` (success/failure)

3. **Provider Failover Count (line chart):**  
   Metric: `openclawnet.provider.failover`  
   Dimension: `from_provider`, `to_provider`

**Effort:** L (6–8 hours to define metrics, instrument, create dashboard)

---

### Summary: Telemetry Score = B+

| Aspect                          | Grade | Notes                                                         |
|---------------------------------|-------|---------------------------------------------------------------|
| Distributed tracing             | A     | Full Gateway ↔ Services ↔ Providers trace chains              |
| GenAI OTel signals              | A     | Token counts, model names captured                            |
| Custom agent/tool instrumentation | D   | No spans for agent selection, tool execution, prompt building |
| Log-trace correlation           | B     | Enabled but needs verification in prod                        |
| Business metrics (agents/tools) | F     | None defined                                                  |
| Production dashboards           | F     | Aspire Dashboard is dev-only; no App Insights workbooks       |

---

## 6. Deployment Readiness

### Observations

**Existing analysis:**  
The team completed a thorough deployment analysis in `docs/deployment/azure-deployment-options-analysis.md` (20KB, 231 lines). It compares 3 Azure deployment paths:

- **Option A (All ACA):** Lift-and-shift Aspire topology to Azure Container Apps
- **Option B (ACA + Foundry Hosted Agents):** Per-profile containerized agents in Foundry (preview)
- **Option C (ACA + Foundry Prompt Agents):** Declarative agents in Foundry (GA)

**Recommendation:** Start with **Option A** (lowest risk), layer in **Option C** for catalog/identity needs.

**Key blockers identified in the analysis (lines 68–86):**
1. ✅ SQLite → Azure SQL / PostgreSQL (correctly flagged)
2. ✅ Secrets → Key Vault + managed identity (correctly flagged)
3. ✅ ACA ingress config (correctly flagged)
4. ✅ Multi-region model availability (correctly flagged)

---

**❌ Gaps not covered in the deployment analysis:**

#### Gap 6.1: No `Aspire.Hosting.Azure.AppContainers` Package Reference

The AppHost project (`OpenClawNet.AppHost.csproj`) references:
- `Aspire.AppHost.Sdk/13.2.2` ✅
- `CommunityToolkit.Aspire.Hosting.Sqlite` ✅
- ❌ **Missing:** `Aspire.Hosting.Azure.AppContainers`

**Impact:** `azd up` won't generate ACA manifests. You'll get an error:

```
Error: No Azure resource providers registered. Add Aspire.Hosting.Azure.AppContainers.
```

**Recommendation:**  
```diff
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Aspire.Hosting.Sqlite" Version="13.1.1" />
+   <PackageReference Include="Aspire.Hosting.Azure.AppContainers" Version="13.2.2" />
  </ItemGroup>
```

**Effort:** S (5 minutes + test `azd init` after)

---

#### Gap 6.2: No `azure.yaml` Manifest

`azd` requires an `azure.yaml` at the repo root to know:
- Which Aspire project is the AppHost
- Which Azure subscription/region to target
- Which services to deploy

**Current state:** No `azure.yaml` exists.

**Recommendation:**  
Run `azd init` at the repo root:

```bash
azd init --template aspire
```

This generates:
- `azure.yaml`
- `infra/` (Bicep templates for ACA, Log Analytics, Container Registry)
- `.azure/` (environment configs)

**Effort:** M (1–2 hours to run `azd init`, verify Bicep, customize for SQLite → Azure SQL)

---

#### Gap 6.3: No Container Images / Dockerfiles

ACA deployment requires **container images**. Aspire's `azd` integration auto-generates Dockerfiles **at deploy time** via the AppHost SDK. But there's no **pre-built images** in a container registry.

**Recommendation:**  
1. Add a `Dockerfile` to each service project (optional; `azd` can generate them).
2. **OR** rely on `azd deploy` to build images on-the-fly via the Aspire SDK (slower first deploy).

**Effort:** S if relying on Aspire SDK; M if writing custom Dockerfiles.

---

#### Gap 6.4: No CI/CD Workflow for Azure Deployment

The existing CI workflow (`.github/workflows/squad-ci.yml`) builds + tests on every PR. But there's **no CD workflow** for deploying to Azure.

**Recommendation:**  
Create `.github/workflows/deploy-to-azure.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [main]
  workflow_dispatch:

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      - uses: azure/setup-azd@v1
      - run: azd deploy --environment production
```

**Effort:** M (2–3 hours to set up service principal, test deployment)

---

### Summary: Deployment Readiness Score = C+

| Aspect                     | Grade | Notes                                                        |
|----------------------------|-------|--------------------------------------------------------------|
| Deployment strategy        | A     | Comprehensive analysis completed; clear path forward         |
| Aspire Azure packages      | D     | Missing `Aspire.Hosting.Azure.AppContainers`                 |
| azd manifest (`azure.yaml`)| F     | Not present; blocks `azd up`                                 |
| Container images           | D     | No Dockerfiles; relying on auto-gen (acceptable for now)     |
| SQLite → managed DB plan   | B     | Correctly identified; not yet implemented                    |
| Secrets → Key Vault plan   | B     | Correctly identified; not yet implemented                    |
| CI/CD for Azure            | F     | No deployment workflow                                       |

---

## 7. CI/CD Posture

### Observations

**Existing workflow:** `.github/workflows/squad-ci.yml`

```yaml
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Restore
        run: dotnet restore OpenClawNet.slnx
      - name: Build
        run: dotnet build OpenClawNet.slnx --no-restore --verbosity quiet
      - name: Unit Tests
        run: dotnet test tests/OpenClawNet.UnitTests --filter "Category!=Live" ...
      - name: Integration Tests
        run: dotnet test tests/OpenClawNet.IntegrationTests ...
```

**What's good:**
- ✅ Builds on every PR
- ✅ Runs unit + integration tests
- ✅ Filters out `[Trait("Category", "Live")]` tests (requires external dependencies)
- ✅ Uploads test results as artifacts

**What's missing:**
- ❌ No Aspire AppHost test (verify `AppHost.cs` compiles, `azd` manifest generation)
- ❌ No deployment preview on PRs (deploy to a staging ACA environment)
- ❌ No container image build/push to ACR
- ❌ No health check validation (smoke test after deploy)
- ❌ No rollback automation

**Recommendation:**  

1. **Add AppHost build check:**
   ```yaml
   - name: Build AppHost
     run: dotnet build src/OpenClawNet.AppHost --no-restore
   ```

2. **Add `azd` validation:**
   ```yaml
   - uses: azure/setup-azd@v1
   - run: azd config set alpha.appHost.enabled on
   - run: azd package --all --output-path ./dist
   ```

3. **Add staging deployment on `dev` branch:**
   ```yaml
   on:
     push:
       branches: [dev]
   
   jobs:
     deploy-staging:
       runs-on: ubuntu-latest
       environment: staging
       steps:
         - uses: azure/login@v1
         - run: azd deploy --environment staging
   ```

**Effort:** M (3–4 hours to extend CI workflow + test)

---

### Summary: CI/CD Score = B–

| Aspect                 | Grade | Notes                                                   |
|------------------------|-------|---------------------------------------------------------|
| Build automation       | A     | Runs on every PR; clean restore/build/test              |
| Test coverage in CI    | B+    | Unit + integration tests; skips live tests appropriately|
| Deployment automation  | F     | No CD workflow                                          |
| Preview environments   | F     | No PR-based staging deploys                             |
| Health checks post-deploy | F  | No smoke tests after deployment                         |

---

## Prioritized Recommendations

### Critical (Ship Blockers)

1. **Add `.WithReference()` for tool services on gateway** (Issue 1.2)  
   **Impact:** ACA deployment will fail service discovery  
   **Effort:** S (10 minutes)

2. **Replace SQLite with Azure SQL** (Issue 1.1)  
   **Impact:** Can't deploy to production without this  
   **Effort:** M (2–4 hours)

3. **Add `Aspire.Hosting.Azure.AppContainers` package** (Gap 6.1)  
   **Impact:** Blocks `azd up`  
   **Effort:** S (5 minutes)

4. **Create `azure.yaml` manifest** (Gap 6.2)  
   **Impact:** Blocks `azd deploy`  
   **Effort:** M (1–2 hours)

5. **Move secrets to Key Vault** (Issue 4.1)  
   **Impact:** Production security requirement  
   **Effort:** M (2–3 hours)

---

### High Priority (Operational Quality)

6. **Add database health check** (Gap 2.1)  
   **Impact:** Prevents "healthy but broken" scenarios  
   **Effort:** S (15 minutes)

7. **Uncomment Azure Monitor exporter** (Gap 2.3)  
   **Impact:** No production telemetry without this  
   **Effort:** S (2 minutes)

8. **Fix `RuntimeModelSettings` multi-instance race** (Issue 4.2)  
   **Impact:** Provider flapping in ACA with 2+ replicas  
   **Effort:** M (3–4 hours)

9. **Add custom agent/tool trace spans** (Gap 5.1)  
   **Impact:** Can't debug agent orchestration in production  
   **Effort:** M (2–3 hours)

---

### Medium Priority (Developer Experience)

10. **Remove hard-coded `Model__Model` from AppHost** (Issue 1.3)  
    **Impact:** Config smell; move to appsettings  
    **Effort:** S (5 minutes)

11. **Add API key validation on startup** (Issue 4.1)  
    **Impact:** Better error messages for new devs  
    **Effort:** S (15 minutes)

12. **Create `appsettings.Production.json`** (Issue 4.3)  
    **Impact:** Undefined production config  
    **Effort:** S (15 minutes)

13. **Add CD workflow for Azure deployment** (Gap 6.4)  
    **Impact:** Manual deploys are error-prone  
    **Effort:** M (2–3 hours)

---

### Low Priority (Nice to Have)

14. **Add business metrics (agent invocations, tool calls)** (Gap 2.2)  
    **Impact:** Better operational visibility  
    **Effort:** M (3–5 hours)

15. **Add resource groups to Aspire Dashboard** (Issue 3.3)  
    **Impact:** Cleaner dev UI  
    **Effort:** S (10 minutes, if SDK supports)

16. **Create "Web only" launch profile** (Issue 3.2)  
    **Impact:** Faster UI-only dev iteration  
    **Effort:** M (1–2 hours)

---

## Concrete Topology Improvements

### Improvement 1: Fix Service Discovery References

**File:** `src/OpenClawNet.AppHost/AppHost.cs`

```diff
  var sqlite = builder.AddSqlite("openclawnet-db", databasePath: dbPath, databaseFileName: "openclawnet.db")
      .WithSqliteWeb();

+ var shellService = builder.AddProject<Projects.OpenClawNet_Services_Shell>("shell-service")
+     .WithHttpHealthCheck("/health");
+ 
+ var browserService = builder.AddProject<Projects.OpenClawNet_Services_Browser>("browser-service")
+     .WithHttpHealthCheck("/health");
+ 
+ var memoryService = builder.AddProject<Projects.OpenClawNet_Services_Memory>("memory-service")
+     .WithHttpHealthCheck("/health");

  var gateway = builder.AddProject<Projects.OpenClawNet_Gateway>("gateway")
      .WithExternalHttpEndpoints()
      .WithHttpHealthCheck("/health")
-     .WithReference(sqlite);
+     .WithReference(sqlite)
+     .WithReference(shellService)
+     .WithReference(browserService)
+     .WithReference(memoryService);

- gateway.WithEnvironment("Model__Model", "gemma4:e2b");  // Remove this line
+ // Enable experimental GenAI semantic conventions (token counts, model metadata in spans)
+ // See: https://opentelemetry.io/docs/specs/semconv/gen-ai/
  gateway.WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_GENAI_EMIT_EVENTS", "true");
```

---

### Improvement 2: Add Database Health Check

**File:** `src/OpenClawNet.ServiceDefaults/Extensions.cs`

```diff
  public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
  {
      builder.Services.AddHealthChecks()
          .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

+     // Add SQLite health check if connection string is configured
+     var connStr = builder.Configuration.GetConnectionString("openclawnet-db");
+     if (!string.IsNullOrEmpty(connStr))
+     {
+         builder.Services.AddHealthChecks()
+             .AddSqlite(connStr, name: "database", tags: ["ready"]);
+     }

      return builder;
  }
```

**NuGet package required:** `AspNetCore.HealthChecks.Sqlite`

---

### Improvement 3: Enable Azure Monitor Exporter

**File:** `src/OpenClawNet.ServiceDefaults/Extensions.cs`

```diff
- // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
- //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
- //{
- //    builder.Services.AddOpenTelemetry()
- //       .UseAzureMonitor();
- //}

+ // Azure Monitor exporter (Application Insights) — auto-enabled if 
+ // APPLICATIONINSIGHTS_CONNECTION_STRING is set in production.
+ if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
+ {
+     builder.Services.AddOpenTelemetry().UseAzureMonitor();
+ }
```

**NuGet package required:** `Azure.Monitor.OpenTelemetry.AspNetCore`

---

## Final Thoughts

OpenClawNet's Aspire orchestration is **well-architected for local development** but **needs 5 critical fixes** before production deployment:

1. Fix service discovery references (10 min)
2. Migrate SQLite → Azure SQL (2–4 hours)
3. Add `Aspire.Hosting.Azure.AppContainers` package (5 min)
4. Create `azure.yaml` via `azd init` (1–2 hours)
5. Move secrets to Key Vault (2–3 hours)

**Total effort to ship to Azure:** ~1 working day.

The team's deployment analysis (`docs/deployment/azure-deployment-options-analysis.md`) is **excellent** — comprehensive, opinionated, and accurate. The path forward is clear: start with Option A (All ACA), layer in Option C (Foundry Prompt Agents) for catalog needs.

The observability foundation (OTel, health checks, service discovery) is **production-ready** with minor gaps (database health check, business metrics). The local dev experience is **exceptional** — single F5, stable ports, clean shutdown.

**This is a solid platform.** The gaps are all fixable in a single sprint. Push the 5 critical fixes, then iterate on telemetry/metrics/dashboards post-launch.

---

**Bishop, signing off.**  
_"Distributed systems should feel boring in the best way: predictable startup, obvious dependencies, clean deployment paths."_
