# 🔎 Demo 3 — Research & archive (web → markdown → embeddings)

**Goal:** in a single agent run, fetch a list of URLs, convert each one to clean Markdown, store the Markdown on disk, and build a tiny semantic index you can query later in the same run with the `embeddings` tool.

**Time to complete:** ~10 minutes (first run downloads the embeddings model, ~90 MB).
**Tools used:** `web_fetch` (or `markdown_convert`), `file_system`, `embeddings`.
**Surface:** Chat — single turn does the whole job.

> 🎯 **Skip the typing — use the built-in template.** Open **Jobs → Job Templates** and pick **Research and archive** (id `research-and-archive`). Replace the three sample URLs with the pages you actually want to archive.

> 💡 **Why this scenario?** It's the smallest possible end-to-end RAG-ish flow with built-in tools only. No vector database, no extra services — just disk + the local embeddings model.

---

## Step 0 — Prepare the archive folder

```powershell
New-Item -ItemType Directory -Path c:\temp\research-archive -Force | Out-Null
```

That's where the agent will write the converted Markdown. The `embeddings` model cache lands automatically under your configured `Storage:RootPath` (default `c:\openclawnet\storage\models\embeddings\`).

---

## Step 1 — Send the prompt

Open the Web UI (`http://localhost:5000`) → **Chat** and paste:

```text
Build me a tiny research archive for these URLs:

- https://elbruno.com
- https://github.com/elbruno/openclawnet-plan
- https://github.com/elbruno/ElBruno.LocalEmbeddings

For each URL:
1. Use markdown_convert to fetch and turn it into clean Markdown.
2. Use file_system action="write" to save the Markdown under
   c:\temp\research-archive\<short-slug>.md (slugify the URL host+first path segment).
3. Take the first 800 characters of the Markdown as the document's "snippet".

Then:
4. Use the embeddings tool with action="search":
   - text  = "How can I run text embeddings locally with .NET?"
   - candidates = the three snippets you just collected
   - topK   = 3
   Report the ranking with scores.

5. Finish with a one-line answer in plain English to that same query, citing the
   winning URL.
```

---

## Step 2 — What you'll see

The chat timeline streams roughly:

```
markdown_convert(url=https://elbruno.com)              → markdown text
file_system(action=write, path=c:\temp\research-archive\elbruno-com.md, ...)
markdown_convert(url=https://github.com/elbruno/openclawnet-plan)
file_system(action=write, ...)
markdown_convert(url=https://github.com/elbruno/ElBruno.LocalEmbeddings)
file_system(action=write, ...)
embeddings(action=search, text="...", candidates=[...], topK=3)
   → 0.78  …LocalEmbeddings README excerpt…
   → 0.41  …openclawnet-plan README excerpt…
   → 0.22  …elbruno.com home page excerpt…
The best answer comes from https://github.com/elbruno/ElBruno.LocalEmbeddings —
it's a .NET library that runs ONNX embedding models locally with zero cloud calls.
```

---

## Step 3 — Confirm the archive

```powershell
Get-ChildItem c:\temp\research-archive
```

You'll see one `.md` per URL. Open any of them — they're plain Markdown, ready to feed into a follow-up tool, a wiki, or your own RAG pipeline.

---

## Variations

- **Make it recurring.** Take this exact prompt and create a Job (`docs/manuals/30-jobs.md`) with cron `0 8 * * 1` to get a fresh archive every Monday morning.
- **Index more aggressively.** Increase the snippet size to e.g. 4000 characters, or chunk each document into N pieces and pass them all as `candidates`.
- **Persist embeddings.** Have the agent also write the candidates list as JSON to disk so a second run can re-rank without re-fetching.

---

## Where to go next

- **[Demo 1](../01-watched-folder-summarizer/README.md)** if you want this triggered by a folder instead of a fixed URL list.
- **[Demo 5](../05-text-to-speech-snippet/README.md)** to send the winning snippet straight to a WAV file with `text_to_speech`.
- **[`docs/manuals/20-tools.md`](../../../manuals/20-tools.md#embeddings)** for the full `embeddings` tool reference.
