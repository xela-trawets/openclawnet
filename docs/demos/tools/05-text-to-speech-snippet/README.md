# 🔊 Demo 5 — Text → speech snippet

**Goal:** in one chat turn, ask the agent to read a paragraph aloud and save the result as a WAV file you can play.

**Time to complete:** ~5 minutes once the model is downloaded (the first run downloads ~5.5 GB into `<Storage:RootPath>\models\qwen-tts\`).
**Tools used:** `text_to_speech`.
**Surface:** Chat (or Tools page → `text_to_speech` → **Direct Invoke**).

> 🎯 **Skip the typing — use the built-in template.** Open **Jobs → Job Templates** and pick **Text-to-speech snippet** (id `text-to-speech-snippet`). Note the prerequisites — first run downloads ~5.5 GB and the tool requires approval by default.

> 💡 **Why this scenario?** It's the simplest possible "approve and run" demo. `text_to_speech` is gated behind tool approval (`RequiresApproval = true`), so this is also a quick way to see the approval flow.

---

## Step 1 — Send the prompt

Web UI → **Chat**:

```text
Please read the following paragraph aloud using the text_to_speech tool with
speaker="serena" and language="english". After the WAV is generated, give me
the absolute path you wrote to.

Text:
"OpenClaw .NET is an agent framework that runs entirely on your machine.
Today we'll show how a single chat turn can produce real audio."
```

---

## Step 2 — Approve the tool call

Because `text_to_speech` requires approval, the agent will pause and show an approval card with the exact arguments it's about to send. Click **Approve**.

You'll see the synthesis run for a few seconds (CPU-bound), then a result line like:

```
Saved to: c:\openclawnet\storage\binary\text-to-speech\20260421-160212-345-serena.wav
Speaker: serena
Language: english
```

---

## Step 3 — Play it

```powershell
# On Windows, default audio app:
Invoke-Item 'c:\openclawnet\storage\binary\text-to-speech\20260421-160212-345-serena.wav'
```

Or open the file in Explorer and double-click it.

---

## Step 4 — (Alternative) Direct Invoke from the Tools page

1. Open the **Tools** page → click the **`text_to_speech`** card.
2. Switch to the **Direct Invoke** tab.
3. Click **Defaults** to pre-fill `text`, `speaker=ryan`, `language=english`.
4. Edit the text if you want, then click **Run**.

The output viewer shows the same `Saved to:` path. Direct Invoke skips the agent and skips the approval card — useful when iterating on the audio itself.

---

## Variations

- Try other voices: `vivian`, `aiden`, `dylan`. The full list is in **[`20-tools.md#text_to_speech`](../../../manuals/20-tools.md#text_to_speech)**.
- Pipe a summary out of **[Demo 1](../01-watched-folder-summarizer/README.md)** — extend that job's prompt with: *"Then call text_to_speech with the bullet summary as `text` so the user can listen to it."* Make sure `text_to_speech` is in the job's **Tools allowed** list and that auto-approval is enabled for the job (jobs run unattended, so they need approvals turned off for these tools — see **[`30-jobs.md`](../../../manuals/30-jobs.md)** for the policy).
- Combine with **[Demo 3](../03-research-and-archive/README.md)** — read the winning snippet of a search out loud.

---

## Where to go next

- **[`docs/manuals/20-tools.md#text_to_speech`](../../../manuals/20-tools.md#text_to_speech)** — full parameters (speakers, languages, approval flag).
- **`Storage:RootPath`** in **[`docs/manuals/20-tools.md#storage-and-secrets`](../../../manuals/20-tools.md#storage-and-secrets)** — change where the WAV files land.
