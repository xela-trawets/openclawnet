# Demo 2 — Error Detection with Copilot CLI + Aspire

This demo introduces **intentional bugs** into the running OpenClawNet app so the presenter can demonstrate how GitHub Copilot CLI + the Aspire Dashboard diagnose and fix real errors.

## How It Works

1. **Before the demo:** Run `introduce-bugs.ps1` to inject 3 bugs into the main solution
2. **During the demo:** Start the app with `aspire run`, show errors in the Aspire Dashboard, use Copilot CLI to diagnose and fix
3. **After the demo:** Run `restore-original.ps1` to restore clean state

## The Bugs

### Bug 1: Wrong Ollama Endpoint (Connection Refused)
- **File:** `src/OpenClawNet.Gateway/appsettings.json`
- **Change:** Ollama endpoint port `11434` → `11435`
- **Symptom:** Chat fails with `HttpRequestException: Connection refused` — visible in Aspire Dashboard traces
- **Fix:** Correct the port number back to `11434`

### Bug 2: Missing Provider DI Registration
- **File:** `src/OpenClawNet.Gateway/Program.cs`
- **Change:** Comment out the `OllamaAgentProvider` singleton registration
- **Symptom:** `InvalidOperationException: No service for type 'OllamaAgentProvider'` — visible in Aspire structured logs
- **Fix:** Uncomment the DI registration line

### Bug 3: Blazor Component Error (Null Reference)
- **File:** `src/OpenClawNet.Web/Components/Layout/NavMenu.razor`
- **Change:** Add a nav item linking to `/broken-page` which doesn't exist, plus a null-reference expression
- **Symptom:** Navigation to the broken page shows a Blazor error boundary — visible as an unhandled exception in Aspire traces
- **Fix:** Remove the broken nav item or create the missing page

## Usage

```powershell
# Inject all 3 bugs
.\introduce-bugs.ps1

# Start the app and observe errors in Aspire Dashboard
aspire run

# After demo — restore clean state
.\restore-original.ps1
```

## Presenter Notes

- Bug 1 is the strongest for the Aspire demo — it produces clear HTTP-level errors in structured traces
- Bug 2 shows a DI container failure — great for showing Copilot diagnosing missing registrations
- Bug 3 is optional — good for showing Blazor error boundaries if time permits
- Use `@terminal fix the errors in the Aspire Dashboard` or similar Copilot CLI prompts
