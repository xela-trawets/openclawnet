# 🐙 Demo 2 — GitHub issue triage (agent run)

**Goal:** ask the agent to look at the open issues in a GitHub repo, fetch the repo's homepage to understand what the project is about, and produce a prioritized triage summary — without writing a single line of glue code.

**Time to complete:** ~10 minutes.
**Tools used:** `github`, `html_query`.
**Surface:** Chat (or **Agent Probe** on the Tools page).

> 🎯 **Skip the typing — use the built-in template.** Open **Jobs → Job Templates** and pick **GitHub issue triage** (id `github-issue-triage`). It seeds the prompt and lists `GITHUB_TOKEN` as a required secret so the UI can flag it. You'll still need to replace the `OWNER/REPO` placeholder.

> 💡 **Why this scenario?** It's the smallest demo that shows two new built-in tools cooperating. Swap the repo + URL and you have a generic "look at this project and tell me what to do" pattern.

---

## Step 0 — (Optional) Set the GitHub token secret

Anonymous GitHub calls work but you'll hit the 60 requests/hour rate limit quickly. Set a token (any read-only PAT will do) so the `github` tool authenticates automatically.

```powershell
$body = '{ "value": "ghp_yourTokenHere", "description": "Read-only PAT for the github tool" }'
Invoke-RestMethod -Method Put `
  -Uri http://localhost:5010/api/secrets/GITHUB_TOKEN `
  -ContentType 'application/json' -Body $body
```

Verify (the list endpoint never returns plaintext):

```powershell
Invoke-RestMethod http://localhost:5010/api/secrets
# → name: GITHUB_TOKEN, description: "Read-only PAT ...", updatedAt: ...
```

The `github` tool reads the secret at execute time and falls back to the `GITHUB_TOKEN` environment variable if no secret is set. See **[ACKNOWLEDGMENTS.md](../../../../ACKNOWLEDGMENTS.md)** and the secrets section of **[`20-tools.md`](../../../manuals/20-tools.md)** for details.

---

## Step 1 — Open the chat

Open the Web UI (`http://localhost:5000`) and click **Chat** in the sidebar. Make sure your active agent profile has both `github` and `html_query` enabled (they are by default for the `system` profile).

---

## Step 2 — Send the prompt

Paste this into the chat box and send:

```text
Triage the open issues in elbruno/openclawnet-plan.

1. Use the github tool (action="list_issues", state="open", perPage=20) to fetch open issues.
2. Use the html_query tool to fetch https://github.com/elbruno/openclawnet-plan
   with selector "article" and limit 1, so you understand what the project is about.
3. Group the issues into three buckets and explain your reasoning briefly:
   - 🔥 Critical (block users / data loss / security)
   - 🟡 Important (visible bugs, missing features)
   - 🟢 Nice-to-have (polish, docs, refactors)
4. Pick the top 3 issues to work on next, with one-line justifications.
5. End with a one-sentence summary of the overall repo health.
```

You should see the agent stream three tool calls (you'll see them in the chat timeline):

1. `github(action=list_issues, ...)` returns a Markdown list of open issues.
2. `html_query(url=https://github.com/elbruno/openclawnet-plan, selector=article)` returns the rendered README content.
3. The agent then reasons over both pieces and writes the triage report.

---

## Step 3 — (Alternative) Run from the Tools page in Agent Probe mode

If you want to invoke just one tool at a time:

1. Open the **Tools** page in the sidebar.
2. Click the **`github`** card → **Agent Probe** tab → use the prompt:
   > *"List the 10 most recent open issues in elbruno/openclawnet-plan"*
3. Click the **`html_query`** card → **Agent Probe** tab → use the prompt:
   > *"What does the H1 of https://elbruno.com say?"*

Agent Probe runs the request through a real agent run with the single tool selected, so you see the prompt → tool call → response chain end-to-end.

---

## Step 4 — Try the variations

A few one-line tweaks that show how composable this is:

- *"Same triage but for `pulls` instead of issues"* — the agent switches `action` to `list_pulls`.
- *"Now fetch the README of `elbruno/elbruno.localembeddings` with the `github` tool (action=get_file, path=README.md) and tell me what model it uses by default"* — combines `get_file` with reasoning.
- *"Get the title (`title` selector) and meta description (`meta[name=description]`, attribute=content) of three recent posts on https://elbruno.com"* — multiple `html_query` calls in one run.

---

## Where to go next

- Wire this same triage prompt into a **scheduled job** ("every Monday at 09:00") so you get a weekly digest. The Jobs UI takes the exact same prompt — see **[Demo 1](../01-watched-folder-summarizer/README.md)**.
- Add the `markdown_convert` tool to the allowed list and ask the agent to also fetch and summarize the linked design doc of any issue that mentions one.
- Read the full reference for the new tools in **[`docs/manuals/20-tools.md`](../../../manuals/20-tools.md)**.
