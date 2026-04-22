# Demo 05 — Skills & Personas

**Level:** 🟡 Intermediate | **Time:** ~5 min  
**Shows:** `ISkillLoader`, enabling/disabling skills, how skills change agent behaviour

---

## Prerequisites

- Gateway running on `http://localhost:5010`
- Ollama running with `llama3.2` pulled

---

## What You'll See

Skills are markdown files with YAML front-matter that inject additional instructions into the system prompt. Enable a skill and the agent adopts new behaviour — disable it and the behaviour disappears. No restart required.

---

## Step 1 — List Available Skills

```powershell
Invoke-RestMethod http://localhost:5010/api/skills | ConvertTo-Json -Depth 4
```

Expected (built-in + samples):
```json
[
  {
    "name": "dotnet-assistant",
    "description": "Helps with .NET development tasks, code reviews, and best practices.",
    "category": "development",
    "enabled": true,
    "tags": ["dotnet", "csharp", "coding"]
  },
  {
    "name": "repo-analyzer",
    "description": "Analyzes repository structure and suggests improvements.",
    "category": "development",
    "enabled": true,
    "tags": ["git", "architecture", "refactoring"]
  },
  {
    "name": "blog-writer",
    "description": "Writes technical blog posts and documentation.",
    "category": "content",
    "enabled": false,
    "tags": ["writing", "markdown", "technical"]
  }
]
```

Skills in `skills/built-in/` start enabled. Skills in `skills/samples/` start disabled.

---

## Step 2 — Chat Without a Persona Skill

Create a baseline session with no extra skills active:

```powershell
# Disable all built-in skills first (to see the baseline)
Invoke-RestMethod http://localhost:5010/api/skills/dotnet-assistant/disable -Method POST
Invoke-RestMethod http://localhost:5010/api/skills/repo-analyzer/disable -Method POST

$s = (Invoke-RestMethod http://localhost:5010/api/sessions -Method POST `
    -ContentType "application/json" -Body '{"title": "Skills demo"}').id

$r = Invoke-RestMethod http://localhost:5010/api/chat -Method POST `
    -ContentType "application/json" `
    -Body (@{ sessionId=$s; message="Show me how to read a file in C#." } | ConvertTo-Json)

$r.content
```

You'll get a generic, unformatted answer.

---

## Step 3 — Enable the dotnet-assistant Skill

```powershell
Invoke-RestMethod http://localhost:5010/api/skills/dotnet-assistant/enable -Method POST
```

Expected:
```json
{ "name": "dotnet-assistant", "enabled": true }
```

Now chat again — same question, new session:

```powershell
$s2 = (Invoke-RestMethod http://localhost:5010/api/sessions -Method POST `
    -ContentType "application/json" -Body '{"title": "With dotnet skill"}').id

$r2 = Invoke-RestMethod http://localhost:5010/api/chat -Method POST `
    -ContentType "application/json" `
    -Body (@{ sessionId=$s2; message="Show me how to read a file in C#." } | ConvertTo-Json)

$r2.content
```

The response now:
- Uses `await File.ReadAllTextAsync()` instead of `File.ReadAllText()`
- Suggests nullable handling
- Recommends `using` declarations
- Follows .NET naming conventions

This is the `dotnet-assistant` skill's instructions taking effect.

---

## Step 4 — Enable the blog-writer Skill

```powershell
Invoke-RestMethod http://localhost:5010/api/skills/blog-writer/enable -Method POST

$s3 = (Invoke-RestMethod http://localhost:5010/api/sessions -Method POST `
    -ContentType "application/json" -Body '{"title": "Blog writer skill"}').id

$r3 = Invoke-RestMethod http://localhost:5010/api/chat -Method POST `
    -ContentType "application/json" `
    -Body (@{ sessionId=$s3; message="Write an intro paragraph about async programming in .NET." } | ConvertTo-Json)

$r3.content
```

The response will be styled as a blog introduction — hook sentence, concrete example, engaging tone.

---

## Step 5 — Reload Skills from Disk

Skills are loaded from markdown files. Edit a skill file and reload without restarting:

```powershell
# Edit skills/built-in/dotnet-assistant.md (add a line)
# Then reload:
Invoke-RestMethod http://localhost:5010/api/skills/reload -Method POST
```

Expected:
```json
{ "reloaded": true, "count": 5 }
```

The next chat immediately uses the updated instructions.

---

## Step 6 — Create Your Own Skill

Add a new file:

```
skills/samples/terse-responder.md
```

```markdown
---
name: terse-responder
description: Responds in the most concise possible way — one sentence maximum.
category: style
enabled: true
tags:
  - concise
  - brevity
---

Respond in ONE sentence only. No bullet points, no code unless the user explicitly asks for it. Be direct and exact.
```

Reload and try it:

```powershell
Invoke-RestMethod http://localhost:5010/api/skills/reload -Method POST

$s4 = (Invoke-RestMethod http://localhost:5010/api/sessions -Method POST `
    -ContentType "application/json" -Body '{"title": "Terse"}').id

(Invoke-RestMethod http://localhost:5010/api/chat -Method POST `
    -ContentType "application/json" `
    -Body (@{ sessionId=$s4; message="What is dependency injection?" } | ConvertTo-Json)
).content
```

You'll get a one-sentence answer.

---

## What Just Happened

```
GetActiveSkillsAsync()
  └─> reads skills/built-in/*.md + skills/samples/*.md
      parses YAML front-matter → SkillDefinition
      filters: enabled=true AND not in _disabledSkills set
      returns SkillContent[] (name + markdown body)

ComposeAsync() [inside every /api/chat call]
  └─> system prompt = base instructions
                    + skill 1 content
                    + skill 2 content
                    + ...
      ← model sees all active skill instructions on every request
```

Skills stack — you can have multiple active at once.

---

## Cleanup

```powershell
# Restore defaults
Invoke-RestMethod http://localhost:5010/api/skills/dotnet-assistant/enable -Method POST
Invoke-RestMethod http://localhost:5010/api/skills/repo-analyzer/enable -Method POST
Invoke-RestMethod http://localhost:5010/api/skills/blog-writer/disable -Method POST
Invoke-RestMethod http://localhost:5010/api/skills/terse-responder/disable -Method POST
```

---

## Next

→ **[Demo 06 — Real-Time Streaming](demo-06-streaming.md)**: stream chat responses live via HTTP SSE and watch tokens arrive in real-time.
