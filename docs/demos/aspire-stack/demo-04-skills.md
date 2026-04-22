# Demo 04 — Skills & Personas

**Level:** 🟡 Intermediate | **Time:** ~5 min  
**Shows:** Runtime skill enable/disable, how skills alter the system prompt, hot-reload from disk

---

## Prerequisites

- OpenClawNet running via AppHost
- Gateway URL: `$gateway = "http://localhost:PORT"`

---

## What You'll See

Skills are markdown files that inject additional instructions into every prompt. Enabling or disabling a skill takes effect immediately on the next message — no restart, no redeployment.

---

## Step 1 — List Available Skills

```powershell
Invoke-RestMethod "$gateway/api/skills" | ConvertTo-Json -Depth 3
```

Built-in skills (in `skills/built-in/`, enabled by default):

| Name | Category | Purpose |
|------|----------|---------|
| `dotnet-assistant` | development | Modern C# conventions, async patterns, DI |
| `repo-analyzer` | development | Architecture analysis, refactoring suggestions |

Sample skills (in `skills/samples/`, disabled by default):

| Name | Category | Purpose |
|------|----------|---------|
| `blog-writer` | content | Technical writing, engaging blog style |
| `azure-helper` | cloud | Azure services, ARM templates, CLI commands |
| `reactor-content-creator` | content | Developer Reactor presentation style |

---

## Step 2 — See Skills in the GenAI Visualizer

With `dotnet-assistant` enabled, open the Aspire Dashboard → Traces → click any chat trace → **GenAI** tab.

In the **Input messages** section you'll see the system prompt. Look for the `dotnet-assistant` block injected after the base system instructions:

```
You are a .NET development assistant. When helping with .NET code:
- Prefer modern C# conventions (nullable reference types, records, pattern matching)
- Suggest async/await patterns where appropriate
...
```

This shows exactly what the model receives every time you chat.

---

## Step 3 — Compare With vs. Without a Skill

**Without** `dotnet-assistant`:

```powershell
Invoke-RestMethod "$gateway/api/skills/dotnet-assistant/disable" -Method POST

$s = (Invoke-RestMethod "$gateway/api/sessions" -Method POST `
    -ContentType "application/json" -Body '{"title": "No skill"}').id

(Invoke-RestMethod "$gateway/api/chat" -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s; message="Show me how to read a file asynchronously in C#." } | ConvertTo-Json)
).content
```

**With** `dotnet-assistant`:

```powershell
Invoke-RestMethod "$gateway/api/skills/dotnet-assistant/enable" -Method POST

$s2 = (Invoke-RestMethod "$gateway/api/sessions" -Method POST `
    -ContentType "application/json" -Body '{"title": "With dotnet skill"}').id

(Invoke-RestMethod "$gateway/api/chat" -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s2; message="Show me how to read a file asynchronously in C#." } | ConvertTo-Json)
).content
```

The second response will:
- Use `await File.ReadAllTextAsync()` instead of sync
- Show `using` declarations, not `using` blocks
- Suggest nullable handling for the path parameter
- Follow the project's naming and style conventions

---

## Step 4 — Enable the blog-writer Skill

```powershell
Invoke-RestMethod "$gateway/api/skills/blog-writer/enable" -Method POST

$s3 = (Invoke-RestMethod "$gateway/api/sessions" -Method POST `
    -ContentType "application/json" -Body '{"title": "Blog style"}').id

(Invoke-RestMethod "$gateway/api/chat" -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s3; message="Write a short intro about streaming LLM responses in .NET." } | ConvertTo-Json)
).content
```

The response will have a blog-like structure: hook opening, concrete example, conversational but technical tone.

---

## Step 5 — Create and Hot-Reload a Custom Skill

Create a new file at `skills/samples/terse.md`:

```markdown
---
name: terse
description: Responds in one sentence only. No lists, no bullet points.
category: style
enabled: true
tags:
  - concise
---

Answer in ONE sentence only. No lists, no code blocks, no formatting. Be direct.
```

Save it, then reload without restarting:

```powershell
Invoke-RestMethod "$gateway/api/skills/reload" -Method POST
```

```json
{ "reloaded": true, "count": 6 }
```

Test it:

```powershell
$s4 = (Invoke-RestMethod "$gateway/api/sessions" -Method POST `
    -ContentType "application/json" -Body '{}').id

(Invoke-RestMethod "$gateway/api/chat" -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s4; message="What is dependency injection?" } | ConvertTo-Json)
).content
```

One sentence. Every time.

---

## Step 6 — See the Reload in the Dashboard

After calling `/api/skills/reload`, check the **Gateway logs** in the Aspire Dashboard:

```
info: OpenClawNet.Skills.FileSkillLoader[0]
      Reloaded 6 skills: dotnet-assistant(enabled), repo-analyzer(enabled),
      blog-writer(enabled), azure-helper(disabled), reactor-content-creator(disabled), terse(enabled)
```

---

## Restore Defaults

```powershell
Invoke-RestMethod "$gateway/api/skills/dotnet-assistant/enable"  -Method POST
Invoke-RestMethod "$gateway/api/skills/repo-analyzer/enable"     -Method POST
Invoke-RestMethod "$gateway/api/skills/blog-writer/disable"      -Method POST
Invoke-RestMethod "$gateway/api/skills/terse/disable"            -Method POST
```

---

## How Skills Work

```
GET /api/skills/reload
  └─> FileSkillLoader.ReloadAsync()
      └─> Reads skills/built-in/*.md + skills/samples/*.md
          Parses YAML front-matter → SkillDefinition { name, enabled, tags, ... }
          Stores markdown body as SkillContent

POST /api/chat (next request)
  └─> IPromptComposer.ComposeAsync()
      └─> GetActiveSkillsAsync()
          └─> Filter: enabled=true AND not in disabled set
          └─> Returns SkillContent[]

      └─> System prompt = base instructions
                        + skill-1 content       ← injected here
                        + skill-2 content
                        + ...
          Messages = [system] + [history] + [user]
```

---

## Next

→ **[Demo 05 — Event-Driven Webhooks](demo-05-webhooks.md)**: trigger agent runs from external events.
