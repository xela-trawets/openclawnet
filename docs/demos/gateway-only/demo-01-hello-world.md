# Demo 01 — Hello World

**Level:** 🟢 Beginner | **Time:** ~2 min  
**Shows:** Gateway health check, version info, OpenAPI spec

---

## What You'll See

The simplest possible confirmation that OpenClawNet is running. No LLM needed.

---

## Steps

### 1. Start the Gateway

```powershell
dotnet run --project src/OpenClawNet.Gateway
```

Wait for:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5010
```

---

### 2. Check Health

```powershell
Invoke-RestMethod http://localhost:5010/health
```

Expected:
```json
{
  "status": "healthy",
  "timestamp": "2026-04-13T18:00:00Z"
}
```

---

### 3. Check Version

```powershell
Invoke-RestMethod http://localhost:5010/api/version
```

Expected:
```json
{
  "version": "0.1.0",
  "name": "OpenClawNet"
}
```

---

### 4. Explore the OpenAPI Spec (Development mode)

```powershell
Invoke-RestMethod http://localhost:5010/openapi/v1.json | ConvertTo-Json -Depth 3
```

Or open in browser: **http://localhost:5010/openapi/v1.json**

You'll see all endpoints documented: Chat, Sessions, Tools, Skills, Memory, Jobs, Webhooks.

---

## What Just Happened

```
HTTP GET /health
└─> Gateway maps GET /health → anonymous handler
    └─> Returns { status, timestamp }
        ← No LLM, no DB, no agent — pure infrastructure
```

The database is created automatically on first start (`openclawnet.db` in the Gateway folder). Check it:

```powershell
Test-Path src/OpenClawNet.Gateway/openclawnet.db
# True
```

---

## Next

→ **[Demo 02 — First Chat](demo-02-first-chat.md)**: send your first message to the LLM.
