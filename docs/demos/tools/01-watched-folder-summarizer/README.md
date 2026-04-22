# 📂 Demo 1 — Watched folder → Markdown → Summary

**Goal:** every 5 minutes, scan `c:\temp\sampleDocs` for any document files. For each one found, convert it to clean Markdown with `markdown_convert`, summarize the content with the agent, and write the summary back next to the original file as `<original>.summary.md`.

**Time to complete:** ~15 minutes.
**Tools used:** `file_system`, `markdown_convert` (+ the agent's own LLM for summarization).
**Surface:** Jobs page (UI-first), with a REST alternative at the end.

> 🎯 **Skip the typing — use the built-in template.** Open the Web UI → **Jobs → Job Templates** and pick **Watched folder → Markdown → Summary** (id `watched-folder-summarizer`). It seeds this exact job for you. The walkthrough below explains *what* each step does, so you understand what the template is doing.

> 💡 **Why this scenario?** It's the smallest possible "intelligent watcher" — a recurring job, a couple of tools, and a summarization step. Once it runs, you can swap any of the three pieces (folder, tool, output) for your own use case.

---

## Step 0 — Prepare the folder

Open PowerShell and create the watched folder plus a couple of sample files:

```powershell
New-Item -ItemType Directory -Path c:\temp\sampleDocs -Force | Out-Null

# Drop in a sample HTML file (the markdown_convert tool also accepts URLs and many other formats)
@'
<html><body>
  <h1>Quarterly review draft</h1>
  <p>Revenue grew 18% YoY driven by the new pricing tier.
     Churn ticked up to 3.2% in November after the price change.
     Action items: revisit the SMB plan and ship the new onboarding flow.</p>
</body></html>
'@ | Set-Content c:\temp\sampleDocs\quarterly-review.html

@'
# Roadmap notes

- Ship watched-folder demo
- Add foundry image generator
- Stabilize the secrets UI
'@ | Set-Content c:\temp\sampleDocs\roadmap.md
```

We'll let the job pick these up automatically.

---

## Step 1 — Open the Jobs page

1. Make sure OpenClaw is running: `aspire start src\OpenClawNet.AppHost`.
2. Open the Web UI (Aspire Dashboard → **OpenClawNet.Web** → **Open Browser**, or go directly to `http://localhost:5000`).
3. In the left sidebar click **Jobs**.

---

## Step 2 — Create the recurring job

Click **+ New Job** and fill in:

| Field | Value |
| --- | --- |
| **Name** | `Folder summarizer (sampleDocs)` |
| **Trigger** | `Cron` |
| **Schedule** | `*/5 * * * *`  *(every 5 minutes)* |
| **Tools allowed** | `file_system`, `markdown_convert` |
| **Timeout** | `300` seconds |
| **Prompt** | the prompt below |

Paste this into the **Prompt** field:

```text
You are a documentation assistant.

1. Use the file_system tool with action="list" and path="c:\temp\sampleDocs" to list every file in that folder.
2. For each file whose name does NOT already end with ".summary.md":
   a. Read the file with file_system action="read".
   b. Convert it to clean Markdown by calling markdown_convert.
      - If the path is a local file, first read it and pass its content; otherwise pass the URL.
   c. Produce a 3–5 bullet summary of the document's content in plain English.
   d. Write the summary to "<original_path>.summary.md" using file_system action="write",
      overwriting any existing file. The file should start with a top-level heading
      "# Summary of <original_filename>" followed by the bullets.
3. If a "<file>.summary.md" already exists for a given file, skip that file.
4. At the end, return a short report:
   - Files scanned
   - Files summarized this run (with paths)
   - Files skipped (already summarized)
```

Click **Save**. The job is now armed and will fire on the next 5-minute boundary.

> 🟢 **Want to verify it works before waiting 5 minutes?** Click the **Run now** button on the job row — it triggers the same execution immediately.

---

## Step 3 — Watch the run

In the Jobs list, click your job's name to open the detail page. Each execution shows up under **History** with:

- start/end timestamps,
- the tool calls the agent made (you'll see `file_system.list`, `markdown_convert`, `file_system.write`),
- the final report from step 4 of the prompt.

If something fails, the run row turns red and the error message is shown inline. Common first-run issues:

| Symptom | Fix |
| --- | --- |
| `Path not allowed` from `file_system` | Make sure `c:\temp\sampleDocs` exists and is writable by the Gateway process. |
| `markdown_convert` hangs on a `.docx` | The tool prefers HTML / URLs / plain text. For `.docx`, read the file first and pass the extracted text, or use a tool that knows the format. |
| `Tool not allowed` | Re-open the job and confirm both `file_system` and `markdown_convert` are checked in **Tools allowed**. |

---

## Step 4 — Confirm the output

Open `c:\temp\sampleDocs` in Explorer (or `Get-ChildItem c:\temp\sampleDocs`) — you should now see, alongside each input file:

- `quarterly-review.html.summary.md`
- `roadmap.md.summary.md`

Each contains a heading and the agent's bullet summary. Drop a new `.html`, `.txt`, or `.md` into the folder; the next run will summarize it and skip the existing summaries.

---

## Step 5 — (Optional) Pause, edit, or delete

From the Jobs list:

- **Pause** stops future executions but keeps the job and its history.
- **Edit** opens the same form you used to create it.
- **Delete** removes the job and its history.

You can also have the agent itself manage the job from chat: *"Pause the `Folder summarizer (sampleDocs)` job"* — it will call `schedule.pause` for you.

---

## REST alternative

If you'd rather create the job programmatically (CI, IaC, scripted setup):

```http
POST http://localhost:5010/api/jobs
Content-Type: application/json

{
  "name": "Folder summarizer (sampleDocs)",
  "trigger": { "type": "cron", "expression": "*/5 * * * *", "timezone": "UTC" },
  "toolsAllowed": ["file_system", "markdown_convert"],
  "timeoutSeconds": 300,
  "prompt": "You are a documentation assistant.\n\n1. Use the file_system tool ..."
}
```

PowerShell one-liner:

```powershell
$body = Get-Content .\watched-folder-job.json -Raw
Invoke-RestMethod -Method Post -Uri http://localhost:5010/api/jobs `
  -ContentType 'application/json' -Body $body
```

A ready-to-post payload lives next to this README at [`watched-folder-job.json`](./watched-folder-job.json).

---

## Where to go next

- Swap `markdown_convert` for `html_query` if you only need a specific selector (e.g. just the `<h1>` of every file).
- Add the `embeddings` tool to the prompt to also build a small in-memory index of summaries — see the upcoming embeddings demo.
- Read **[`docs/manuals/30-jobs.md`](../../../manuals/30-jobs.md)** for the full Jobs reference (retry policies, timezones, history retention).
