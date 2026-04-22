# 🤖 Session 2: Copilot Prompts

Two live Copilot moments that demonstrate AI-assisted development within the tool framework.

---

## Prompt 1: Add Blocked Commands to ShellTool

### When
**Stage 2** (~minute 22) — after walking through the ShellTool blocklist

### Context
- **File open:** `src/OpenClawNet.Tools.Shell/ShellTool.cs`
- **Cursor position:** Inside the `BlockedCommands` HashSet initializer
- **What just happened:** We explained the command blocklist and `IsSafeCommand` validation

### Mode
**Copilot Chat** (inline or sidebar)

### Exact Prompt

```
Add `wget` and `curl` to the blocked commands list in the ShellTool. These tools could be used to exfiltrate data from the server. Also add a comment explaining why network tools are blocked.
```

### Expected Result

Copilot adds `"wget"` and `"curl"` to the `BlockedCommands` HashSet and adds a comment:

```csharp
private static readonly HashSet<string> BlockedCommands = new(StringComparer.OrdinalIgnoreCase)
{
    // Destructive commands
    "rm", "del", "format", "fdisk", "mkfs", "dd", "shutdown", "reboot",
    "kill", "taskkill", "net", "reg", "regedit", "powershell", "cmd",
    // Network tools blocked to prevent data exfiltration
    "wget", "curl"
};
```

### Why It's Interesting

- **Small, focused change** — reinforces the security-first mindset
- **Architectural payoff** — extending the defense is trivial because the blocklist is a simple data structure
- **Real-world relevance** — `wget` and `curl` are actual attack vectors for data exfiltration in agent systems
- **Shows Copilot understanding context** — it knows this is a security blocklist and generates an appropriate comment

---

## Prompt 2: Add Execution Duration Tracking to ToolExecutor

### When
**Stage 3** (~minute 40) — after walking through the agent loop and tool execution

### Context
- **File open:** `src/OpenClawNet.Tools.Core/ToolExecutor.cs`
- **Cursor position:** Below the existing `ExecuteAsync` method
- **What just happened:** We showed the agent loop executing tools, each already timed with `Stopwatch`

### Mode
**Copilot Chat** (sidebar recommended — larger generation)

### Exact Prompt

```
In the ToolExecutor, add a method `GetExecutionStats()` that returns a dictionary of tool name → average execution duration. Track each tool's execution duration in a `ConcurrentDictionary<string, List<TimeSpan>>` field. Update it after each successful execution.
```

### Expected Result

Copilot generates:
1. A new `ConcurrentDictionary<string, List<TimeSpan>>` field
2. Code in `ExecuteAsync` to record duration after successful execution
3. A `GetExecutionStats()` method that calculates averages

```csharp
private readonly ConcurrentDictionary<string, List<TimeSpan>> _executionTimes = new();

// Inside ExecuteAsync, after successful execution:
_executionTimes.AddOrUpdate(
    toolName,
    _ => new List<TimeSpan> { sw.Elapsed },
    (_, list) => { list.Add(sw.Elapsed); return list; });

public IReadOnlyDictionary<string, TimeSpan> GetExecutionStats()
{
    return _executionTimes.ToDictionary(
        kvp => kvp.Key,
        kvp => TimeSpan.FromTicks((long)kvp.Value.Average(t => t.Ticks)));
}
```

### Why It's Interesting

- **Chokepoint pattern payoff** — because all tools flow through the executor, we add metrics in ONE place and get stats for ALL tools
- **Cross-cutting concern** — demonstrates how good architecture makes observability trivial
- **Real-world application** — production agents need execution metrics for monitoring and optimization
- **Copilot reads existing patterns** — it sees the `Stopwatch` already in the code and extends it naturally
