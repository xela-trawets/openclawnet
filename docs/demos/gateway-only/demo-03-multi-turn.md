# Demo 03 — Multi-Turn Conversation

**Level:** 🟡 Intermediate | **Time:** ~5 min  
**Shows:** Session management, message history, conversation continuity

---

## Prerequisites

- Gateway running on `http://localhost:5010`
- Ollama running with `llama3.2` pulled

---

## What You'll See

How OpenClawNet maintains conversation context across multiple turns. Each message retrieves full history from SQLite, and the model sees the entire conversation every time.

---

## Steps

### 1. List Existing Sessions

```powershell
Invoke-RestMethod http://localhost:5010/api/sessions | ConvertTo-Json -Depth 3
```

You'll see sessions from earlier demos (if any). Each has `id`, `title`, `createdAt`, `updatedAt`.

---

### 2. Start a Fresh Session

```powershell
$s = Invoke-RestMethod http://localhost:5010/api/sessions `
    -Method POST `
    -ContentType "application/json" `
    -Body '{"title": "Programming conversation"}'
$sid = $s.id
```

---

### 3. Build a Multi-Turn Conversation

Send three messages that build on each other:

```powershell
# Turn 1 — set up context
(Invoke-RestMethod http://localhost:5010/api/chat `
    -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$sid; message="I'm learning C#. I know Python well." } | ConvertTo-Json)
).content

# Turn 2 — model must remember Turn 1
(Invoke-RestMethod http://localhost:5010/api/chat `
    -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$sid; message="What's the C# equivalent of Python's list comprehension?" } | ConvertTo-Json)
).content

# Turn 3 — continue the topic
(Invoke-RestMethod http://localhost:5010/api/chat `
    -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$sid; message="Show me a practical example using a list of numbers." } | ConvertTo-Json)
).content
```

Notice Turn 2 answers in a Python-to-C# translation style — because the agent remembered Turn 1.

---

### 4. Inspect the Message History

```powershell
Invoke-RestMethod "http://localhost:5010/api/sessions/$sid/messages" |
    ForEach-Object { Write-Host "[$($_.role.ToUpper())] $($_.content.Substring(0, [Math]::Min(80, $_.content.Length)))..." }
```

Expected output:
```
[USER] I'm learning C#. I know Python well....
[ASSISTANT] Great! Here's what you should know coming from Python......
[USER] What's the C# equivalent of Python's list comprehension?...
[ASSISTANT] In C#, LINQ provides the equivalent functionality...
[USER] Show me a practical example using a list of numbers....
[ASSISTANT] Here's an example using LINQ to filter and transform...
```

---

### 5. Inspect a Full Session Object

```powershell
Invoke-RestMethod "http://localhost:5010/api/sessions/$sid" | ConvertTo-Json -Depth 5
```

Returns the session with all messages embedded — useful for exporting or auditing conversations.

---

### 6. Rename the Session

```powershell
Invoke-RestMethod "http://localhost:5010/api/sessions/$sid/title" `
    -Method PATCH `
    -ContentType "application/json" `
    -Body '{"title": "C# from Python perspective"}'
```

---

### 7. Run Two Parallel Conversations

Sessions are independent. Try running two at the same time to see isolation:

```powershell
$s2 = (Invoke-RestMethod http://localhost:5010/api/sessions -Method POST -ContentType "application/json" -Body '{"title": "Cooking chat"}').id

# Both sessions are active simultaneously
Invoke-RestMethod http://localhost:5010/api/chat -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$sid; message="What is Task<T> in C#?" } | ConvertTo-Json) | Select-Object content

Invoke-RestMethod http://localhost:5010/api/chat -Method POST -ContentType "application/json" `
    -Body (@{ sessionId=$s2; message="How do I make pasta carbonara?" } | ConvertTo-Json) | Select-Object content
```

Each session maintains completely separate context.

---

### 8. Delete a Session

```powershell
Invoke-RestMethod "http://localhost:5010/api/sessions/$s2" -Method DELETE
```

Response: `204 No Content`. The session and all its messages are removed from SQLite.

---

## What Just Happened

```
POST /api/sessions               ← create: INSERT into chat_sessions table
POST /api/chat (Turn 1)
  └─> GetMessagesAsync()         ← SELECT WHERE session_id = $sid (0 messages)
      ComposeAsync()             ← [system] + [user: "I'm learning C#..."]
      CompleteAsync()            ← Ollama responds
      AddMessageAsync()          ← INSERT user + assistant messages

POST /api/chat (Turn 2)
  └─> GetMessagesAsync()         ← SELECT WHERE session_id = $sid (2 messages)
      ComposeAsync()             ← [system] + [user1] + [assistant1] + [user2]
      CompleteAsync()            ← Ollama sees full history
      AddMessageAsync()          ← INSERT 2 more messages
```

History grows linearly. When it gets too long, `DefaultSummaryService` compresses older turns into a summary block (auto-managed).

---

## Next

→ **[Demo 04 — Tool Use](demo-04-tool-use.md)**: ask the agent to read and list files — watch it call tools automatically.
