# 📄 Scenario 1: Document Processing Pipeline

## Overview

A **scheduled job** monitors a watched folder, automatically processes incoming documents, generates AI-powered summaries, and stores results in a searchable vector index. This scenario teaches the complete flow of background job orchestration: scheduling, file system operations, skill composition, and streaming notifications.

**Time to complete:** 20–30 minutes  
**Technologies:** Scheduler, File System Tools, Skills (Markdown Conversion + Summarization), Vector Embeddings, HTTP SSE Streaming  
**Prerequisites:** Aspire or Gateway running, Ollama ready, sample documents in `docs/sampleDocs/`

💡 **The webapp provides the easiest way to launch and monitor this demo. The API is available for scripting and automation.**

---

## What You'll Learn

- ✅ Schedule a recurring job (Scheduler service)
- ✅ Read and monitor files from disk (File System Tool)
- ✅ Compose multiple skills into a workflow (Markdown Converter + Summarizer)
- ✅ Store and query embeddings (Vector Index)
- ✅ Stream job progress in real-time (HTTP SSE)
- ✅ Handle files through completion and archiving

---

## Quick Start

### UI-First: Launch from the Webapp

**Step 1: Open the Webapp**

Navigate to the Blazer app from the Aspire dashboard, or go directly to `http://localhost:5000`.

**Step 2: Click "Jobs" in Sidebar**

In the main navigation, select the **Jobs** page. This is the central hub for viewing, managing, and launching demo scenarios.

**Step 3: Find the "Document Processing Pipeline" Card**

Scroll to the **Demo Templates** section (at the top of the Jobs page). Look for the card labeled "📋 Document Processing Pipeline" with a description: "Auto-processes documents, generates summaries, builds searchable index."

**Step 4: Click "🚀 Launch Demo"**

Click the **Launch Demo** button on the card. The button POSTs to `/api/demos/doc-pipeline/setup` with default settings:
- **Schedule:** Every 5 minutes (cron: `0 */5 * * * *`)
- **Watch Folder:** `docs/sampleDocs/`
- **Output Folder:** `docs/sampleDocs/_processed/`
- **Archive Folder:** `docs/sampleDocs/_archive/`

The page briefly shows a loading state, then returns to the Jobs page.

**Step 5: Job Appears in the List**

Below the Demo Templates section, you'll see the jobs list. Your new job appears as:
- **Name:** Document Processing Pipeline
- **Status:** 🟢 Active
- **Created:** Just now
- **Next Run:** ~5 minutes from now

**Step 6: Monitor Status**

The job card shows real-time status:
- 🔄 **Running** — currently processing files
- 🟢 **Active** — scheduled, waiting for next trigger
- ✅ **Completed** — run finished, click to view details

Click the job card to expand and see:
- Files processed in the last run
- Summary snippets from processed documents
- Last run time and next scheduled run
- Real-time status updates via HTTP polling

---

## How It Works: Step-by-Step Flow

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│  Scheduler Service (IJobScheduler)                           │
│  Trigger: Every 5 minutes (cron: 0 */5 * * * *)              │
└────────────────────┬────────────────────────────────────────┘
                     │ fires
┌────────────────────▼────────────────────────────────────────┐
│  Job Runner: DocumentProcessingJob                           │
│  State: Running (durable in EF Core SQLite)                  │
└────────────────────┬────────────────────────────────────────┘
                     │
          ┌──────────┴──────────┐
          ▼                     ▼
    ┌────────────┐         ┌────────────┐
    │ Step 1:    │         │ Step 2:    │
    │ Discover   │         │ Read       │
    │ Files      │         │ Content    │
    └─────┬──────┘         └────┬───────┘
          │                     │
          │ File System Tool    │ File System Tool
          │ lists: docs/        │ reads: invoice.pdf
          │ sampleDocs/         │ output: raw bytes
          │                     │
          ├─────────────────────┤
          │                     │
          ▼                     ▼
    ┌────────────────────────────────┐
    │ Step 3: Convert to Markdown    │
    │ Skill: MarkdownConverterSkill  │
    │ input: raw bytes               │
    │ output: clean markdown text    │
    └────────────┬───────────────────┘
                 │
                 ▼
    ┌────────────────────────────────┐
    │ Step 4: Generate Summary       │
    │ Skill: SummarizationSkill      │
    │ (uses Chat API + Ollama)       │
    │ input: markdown text           │
    │ output: 2-3 sentence summary   │
    └────────────┬───────────────────┘
                 │
                 ▼
    ┌────────────────────────────────┐
    │ Step 5: Generate Embedding     │
    │ Service: EmbeddingsService     │
    │ input: summary + full text     │
    │ output: vector (768-dim)       │
    └────────────┬───────────────────┘
                 │
                 ▼
    ┌────────────────────────────────┐
    │ Step 6: Store Results          │
    │ - Save to Vector Index (SQLite)│
    │ - Archive original document    │
    │ - Log job run metrics          │
    └────────────────────────────────┘
```

### Detailed Steps

**Step 1: Scheduler fires (every 5 min)**
```csharp
// Built into IJobScheduler
// CronSchedule: "0 */5 * * * *"
// Checks: Any unprocessed docs in docs/sampleDocs/?
```

**Step 2: Job lists files**
```bash
GET docs/sampleDocs/
→ [invoice.pdf, report.docx, policy.pdf, contract.txt, memo.md]
→ Filter out: already processed (in _archive/)
→ Remaining: [invoice.pdf, report.docx]
```

**Step 3: For each file, read content**
```csharp
var content = FileSystemTool.ReadFile("docs/sampleDocs/invoice.pdf");
// returns: byte[] or string (based on file type)
```

**Step 4: Convert to Markdown**
```csharp
var mdContent = await markdownConverterSkill.Execute(
  new { 
    filePath = "invoice.pdf",
    content = content
  }
);
// input: raw PDF/DOCX bytes
// output: clean markdown text
// Examples:
//   invoice.pdf → tables → markdown | → | format
//   report.docx → formatted text → markdown headings
```

**Step 5: Generate summary**
```csharp
var summary = await summarizationSkill.Execute(
  new {
    text = mdContent,
    maxLength = 150 // tokens
  }
);
// Calls Chat API with system prompt:
//   "Summarize this document in 2-3 sentences. Be specific."
// Uses Ollama (local) or Azure OpenAI (configured in .env)
```

**Step 6: Create embedding**
```csharp
var embedding = await embeddingsService.EmbedAsync(
  new { 
    text = $"{summary}\n\n{mdContent.Substring(0, 2000)}"
  }
);
// Returns: float[] (768 dimensions)
// Stored in SQLite vector column
```

**Step 7: Store result**
```csharp
await documentIndexStore.InsertAsync(new DocumentRecord
{
  DocumentId = Guid.NewGuid(),
  Filename = "invoice.pdf",
  Summary = summary,
  FullText = mdContent,
  Embedding = embedding,
  ProcessedAt = DateTime.UtcNow,
  JobRunId = job.Id
});

// Archive original
File.Move(
  "docs/sampleDocs/invoice.pdf",
  "docs/sampleDocs/_archive/invoice.pdf"
);
```

---

## Sample Documents Included

The `docs/sampleDocs/` folder contains 5 example documents to process:

| File | Type | Content | Use Case |
|------|------|---------|----------|
| `invoice.pdf` | PDF | Multi-page invoice with tables | Financial document processing |
| `report.docx` | Word Doc | Formatted report with charts | Complex structured data |
| `policy.pdf` | PDF | Legal compliance document | Regulated text |
| `contract.txt` | Plain text | Service agreement | Unstructured text |
| `memo.md` | Markdown | Internal notes | Already-structured content |

**Expected outputs:**
```
invoice.pdf   → "Invoice #INV-2026-0142 from Acme Corp totaling $8,500..."
report.docx   → "Q1 Performance Report: Revenue up 23%, customer up 18%..."
policy.pdf    → "Policy effective 2026-01-01 governs use of services..."
contract.txt  → "This agreement is between Client and Provider..."
memo.md       → "Meeting notes from 2026-04-16: Discussed Q2 roadmap..."
```

---

## Skill Composition Explained

This scenario demonstrates **skill composition** — chaining 2+ skills to solve a larger problem:

```
Input Document
    ↓
    └─→ Markdown Converter Skill
            (PDF/DOCX → plain text + markdown)
            ↓
            └─→ Summarization Skill
                    (long text → short summary)
                    ↓
                    └─→ Embedding Service
                            (text → vector)
                            ↓
                            └─→ Vector Index Store
                                    (vector → searchable)
```

Each skill is independent and testable:

```csharp
// Test just the converter
[Test]
public async Task MarkdownConverter_ConvertsPdfToMarkdown()
{
  var pdf = File.ReadAllBytes("samples/invoice.pdf");
  var md = await skill.Execute(new { content = pdf });
  Assert.That(md, Does.Contain("# Invoice"));
}

// Test just the summarizer
[Test]
public async Task Summarizer_GeneratesConsiseSummary()
{
  var text = "This is a very long document..."; // 5000 words
  var summary = await skill.Execute(new { text = text });
  Assert.That(summary.Split(' ').Length, Is.LessThan(50));
}
```

---

## Extending the Scenario

### Add Email Notifications
```csharp
// Extend the job to send email after processing
var emailSkill = container.GetRequiredService<EmailNotificationSkill>();
await emailSkill.Execute(new
{
  to = "manager@company.com",
  subject = $"Document Processing Complete: {filesProcessed} docs",
  body = $"Processed {filesProcessed} documents. Results in dashboard."
});
```

### Query the Vector Index
```bash
# Find documents similar to a query
curl -X POST http://localhost:5010/api/demos/doc-pipeline/search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "What financial documents were received this month?",
    "topK": 3
  }'

# Response: Top 3 most relevant documents + similarity scores
```

### Set Up Recurring Cleanup
```bash
# Archive documents older than 30 days
curl -X POST http://localhost:5010/api/demos/doc-pipeline/cleanup \
  -d '{ "retentionDays": 30 }'
```

---

## Troubleshooting

### ❌ "File not found: docs/sampleDocs/"
**Solution:** Sample docs are included with the repo. If missing:
```bash
# From repo root
mkdir -p docs/sampleDocs
mkdir -p docs/sampleDocs/_processed
mkdir -p docs/sampleDocs/_archive
```

### ❌ "Scheduler never fires (status always 'scheduled')"
**Solution:** Check Gateway is running and Scheduler service is enabled:
```bash
curl http://localhost:5010/health
# Should show ✅ scheduler: healthy
```

### ❌ "Ollama timeout during summarization"
**Solution:** Pre-warm the model:
```bash
ollama pull gemma4:e2b
ollama show gemma4:e2b
# Then retry
```

### ❌ "Vector embedding failed"
**Solution:** Embeddings service requires a running model. Check `.env`:
```bash
EMBEDDINGS_PROVIDER=ollama  # or: azure_openai
OLLAMA_BASE_URL=http://localhost:11434
```

---

## Key Concepts

### IJobScheduler
The central scheduling engine. Stores job definitions and runs in the background.
```csharp
public interface IJobScheduler
{
  Task<string> ScheduleAsync(JobDefinition definition);
  Task<JobStatus> GetStatusAsync(string jobId);
  Task CancelAsync(string jobId);
}
```

### IJobRun
Tracks a single execution of a job. Durable—persisted in SQLite.
```csharp
public class JobRun
{
  public string Id { get; set; } // "doc-pipeline-job-001_run_2026-04-20T14:05:00Z"
  public DateTime StartedAt { get; set; }
  public DateTime? CompletedAt { get; set; }
  public string Status { get; set; } // running, succeeded, failed
  public int FilesProcessed { get; set; }
  public List<string> Errors { get; set; }
}
```

### Skills as Composable Units
Skills encapsulate domain logic and can be chained:
```csharp
public interface ISkill
{
  string Name { get; }
  Task<object> ExecuteAsync(Dictionary<string, object> inputs);
}
```

---

## Next Scenario

Once you've completed this scenario:
- 📚 Review the job runs in the database (inspect SQLite with DBeaver or `dotnet run --project tools/sqlite-inspector`)
- 🔗 Ready for **Scenario 2: Event-Driven Conversation Kickoff**? This scenario uses job completion events to trigger the next workflow.

---

## API Alternative (For Advanced Users & CI/CD)

The webapp UI is the easiest way to launch and monitor this demo. If you need to script the demo setup or integrate it into CI/CD pipelines, use the HTTP API directly.

### Set Up via API

Create the document processing job:

```bash
curl -X POST http://localhost:5010/api/demos/doc-pipeline/setup \
  -H "Content-Type: application/json" \
  -d '{
    "schedule": "0 */5 * * * *",
    "watchFolder": "docs/sampleDocs/",
    "outputFolder": "docs/sampleDocs/_processed/",
    "archiveFolder": "docs/sampleDocs/_archive/"
  }'
```

**Expected response:**
```json
{
  "jobId": "doc-pipeline-job-001",
  "status": "scheduled",
  "nextRun": "2026-04-20T14:05:00Z",
  "message": "Document processing job scheduled. Will run every 5 minutes."
}
```

### Check Job Status via API

Monitor the job progress:

```bash
curl http://localhost:5010/api/demos/doc-pipeline/status
```

**Expected response:**
```json
{
  "jobId": "doc-pipeline-job-001",
  "status": "running",
  "filesProcessed": 3,
  "lastRun": "2026-04-20T14:05:00Z",
  "nextRun": "2026-04-20T14:10:00Z",
  "currentDocument": "invoice.pdf",
  "progress": 60
}
```

### View Processed Results via API

Retrieve summaries and metadata:

```bash
curl http://localhost:5010/api/demos/doc-pipeline/results
```

**Expected response:**
```json
{
  "documents": [
    {
      "filename": "invoice.pdf",
      "status": "completed",
      "processedAt": "2026-04-20T14:05:32Z",
      "summary": "Invoice #INV-2026-0142 from Acme Corp totaling $8,500 for consulting services rendered March 2026. Net 30 terms.",
      "extractedTopics": ["finance", "invoice", "services", "payment"],
      "embedding": "0.234,-0.891,0.456,...",
      "wordCount": 1247
    },
    {
      "filename": "report.docx",
      "status": "completed",
      "processedAt": "2026-04-20T14:06:15Z",
      "summary": "Q1 Performance Report: Revenue up 23%, customer acquisition up 18%. Key risks: supply chain delays, competitive pressure.",
      "extractedTopics": ["performance", "finance", "risk", "strategy"],
      "embedding": "0.562,-0.123,0.789,...",
      "wordCount": 3892
    }
  ],
  "totalProcessed": 2,
  "indexSize": 2
}
```

### Stream Real-Time Updates via API (Optional)

Monitor job progress via polling or HTTP SSE:

```bash
# Poll for job status every 5 seconds
while true; do
  curl -s http://localhost:5010/api/demos/doc-pipeline/status | jq '.progress'
  sleep 5
done
```

Or use Server-Sent Events (SSE) if your application supports it:

```javascript
// JavaScript / Node.js example - polling approach
const pollJobProgress = async () => {
  const interval = setInterval(async () => {
    const response = await fetch("http://localhost:5010/api/demos/doc-pipeline/status");
    const data = await response.json();
    
    console.log(`Processing: ${data.currentDocument} (${data.progress}%)`);
    
    if (data.status === "completed") {
      clearInterval(interval);
      console.log(`✅ Job completed: ${data.filesProcessed} files processed`);
    }
  }, 2000); // Poll every 2 seconds
};

await pollJobProgress();
```

---

## References

- [Scheduler Documentation](../../architecture/scheduler.md)
- [Skills Guide](../../skills/README.md)
- [File System Tool](../../tools/file-system-tool.md)
- [Vector Embeddings](../../architecture/embeddings.md)
