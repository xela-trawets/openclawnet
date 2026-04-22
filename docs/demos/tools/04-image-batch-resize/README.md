# 🖼️ Demo 4 — Image batch resize (scheduled job)

**Goal:** every hour, take any image dropped into `c:\temp\images-in\` and produce a 256×256 WebP thumbnail in `c:\temp\images-out\`. Skip files that already have a matching thumbnail.

**Time to complete:** ~10 minutes.
**Tools used:** `file_system`, `image_edit`.
**Surface:** Jobs page (UI-first), with REST alternative.

> 🎯 **Skip the typing — use the built-in template.** Open **Jobs → Job Templates** and pick **Image batch resize (hourly)** (id `image-batch-resize`).

> 💡 **Why this scenario?** Same shape as Demo 1 (folder-watcher), but it shows the `image_edit` tool and demonstrates that a job can write its outputs to the central `Storage:RootPath` instead of next to the source.

---

## Step 0 — Prepare folders + a sample image

```powershell
New-Item -ItemType Directory -Path c:\temp\images-in  -Force | Out-Null
New-Item -ItemType Directory -Path c:\temp\images-out -Force | Out-Null

# Copy any local PNG/JPG into the input folder, e.g.:
Copy-Item "C:\Windows\Web\Wallpaper\Windows\img0.jpg" c:\temp\images-in\
```

Drop one or two more images of your choice into `c:\temp\images-in\` if you have them handy.

---

## Step 1 — Create the job

Open the Web UI → **Jobs** → **+ New Job** and fill:

| Field | Value |
| --- | --- |
| **Name** | `Thumbnail builder` |
| **Trigger** | `Cron` |
| **Schedule** | `0 * * * *`  *(top of every hour — use `*/5 * * * *` to test faster)* |
| **Tools allowed** | `file_system`, `image_edit` |
| **Timeout** | `300` seconds |
| **Prompt** | (see below) |

```text
You are a thumbnail batch worker.

1. Use file_system action="list" with path="c:\temp\images-in" to enumerate every file.
2. For each file with an extension of .jpg, .jpeg, .png, or .webp:
   a. Compute the output path:
      "c:\temp\images-out\" + <filename without extension> + "-256.webp"
   b. Use file_system action="exists" to check whether that output already exists.
      If it does, skip the file (do NOT regenerate).
   c. Otherwise call image_edit with:
        action  = "resize"
        input   = the full input path
        format  = "webp"
        width   = 256
        height  = 256
      The tool will create the file under <Storage>/binary/image-edit/<timestamp>-resize.webp.
   d. Read the absolute path from the tool result (the line starting with "Saved to:")
      and use file_system action="read" + action="write" — or, if available, a move —
      to place it at the output path computed in step (a).
3. Return a summary: files scanned, thumbnails created (with paths), files skipped.
```

Click **Save**, then **Run now** to fire it once without waiting.

---

## Step 2 — Verify

```powershell
Get-ChildItem c:\temp\images-out
```

You should see one `<name>-256.webp` per input image. Drop another image into `c:\temp\images-in\` and either click **Run now** or wait for the next cron tick — only the new file gets processed.

> ℹ️ **Where do the originals from `image_edit` live?** The `image_edit` tool always writes a copy to `<Storage:RootPath>\binary\image-edit\` (default `c:\openclawnet\storage\binary\image-edit\`) using a timestamped name. The job above reads that copy and places it at the friendly output path. You can prune the staging folder periodically with another small job if you wish.

---

## REST alternative

```powershell
$body = @'
{
  "name": "Thumbnail builder",
  "trigger": { "type": "cron", "expression": "0 * * * *", "timezone": "UTC" },
  "toolsAllowed": ["file_system", "image_edit"],
  "timeoutSeconds": 300,
  "prompt": "You are a thumbnail batch worker. ... (full text from Step 1)"
}
'@
Invoke-RestMethod -Method Post -Uri http://localhost:5010/api/jobs `
  -ContentType 'application/json' -Body $body
```

---

## Variations

- Add **`format=jpeg`** to also produce a JPEG variant for legacy clients.
- Use **`action=crop`** with a fixed aspect ratio for square avatars.
- Combine with **Demo 1** so the job also writes a tiny `<name>.txt` caption for each new image (run the agent through `markdown_convert`-of-EXIF + summarize).

---

## Where to go next

- **[`docs/manuals/20-tools.md#image_edit`](../../../manuals/20-tools.md#image_edit)** — full `image_edit` reference (resize/convert/crop, supported formats, x/y crop origin).
- **[`docs/manuals/30-jobs.md`](../../../manuals/30-jobs.md)** — retry policies and timezones for production schedules.
