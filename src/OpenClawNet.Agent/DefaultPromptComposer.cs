using Microsoft.Extensions.Options;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.Agent;

public sealed class DefaultPromptComposer : IPromptComposer
{
    private readonly IWorkspaceLoader _workspaceLoader;
    private readonly WorkspaceOptions _workspaceOptions;

    private string BuildFallbackSystemPrompt() => $"""
        You are OpenClaw .NET, an AI agent built with .NET and Microsoft Agent Framework.
        You help with coding, file operations, project analysis, and software development tasks.

        ## Workspace
        Your current workspace root is: {_workspaceOptions.WorkspacePath}

        ## Tool Usage Rules
        You have access to tools. Use them proactively when they help answer the user's request:

        - **file_system**: Use whenever asked about files, directories, or .NET projects on the local machine.
          - To find .NET projects in a solution: action=find_projects, path=<directory or '.' for workspace>
          - To list a directory: action=list, path=<directory>
          - To read a file: action=read, path=<file path>
          - IMPORTANT: If the user says "in <path>" or "at <path>", pass that path to the tool directly.
          - Prefer calling the tool over asking the user to provide file contents manually.

        - **web_search**: Use for current events, documentation lookups, or anything requiring up-to-date info.

        ## Response Style
        Be concise and accurate. When using tools, show what you found. 
        If you cannot find something with one tool call, try a different path or approach.
        Do NOT ask the user to paste file contents — use the file_system tool to read them yourself.
        """;

    public DefaultPromptComposer(
        IWorkspaceLoader workspaceLoader,
        IOptions<WorkspaceOptions> workspaceOptions)
    {
        _workspaceLoader = workspaceLoader;
        _workspaceOptions = workspaceOptions.Value;
    }

    public async Task<IReadOnlyList<ChatMessage>> ComposeAsync(PromptContext context, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>();

        // 1. Load workspace bootstrap files
        var bootstrap = await _workspaceLoader.LoadAsync(_workspaceOptions.WorkspacePath, cancellationToken);

        // 2. Build system prompt — prefer AGENTS.md persona, fall back to built-in default
        var systemContent = !string.IsNullOrWhiteSpace(bootstrap.AgentsMd)
            ? bootstrap.AgentsMd
            : BuildFallbackSystemPrompt();

        // 3. Append core values from SOUL.md
        if (!string.IsNullOrWhiteSpace(bootstrap.SoulMd))
        {
            systemContent += $"\n\n# Core Values\n{bootstrap.SoulMd}";
        }

        // 4. Append user profile from USER.md
        if (!string.IsNullOrWhiteSpace(bootstrap.UserMd))
        {
            systemContent += $"\n\n# User Profile\n{bootstrap.UserMd}";
        }

        // 5. Add session summary if available
        if (!string.IsNullOrEmpty(context.SessionSummary))
        {
            systemContent += $"\n\n# Previous Conversation Summary\n{context.SessionSummary}";
        }

        messages.Add(new ChatMessage { Role = ChatMessageRole.System, Content = systemContent });

        // 6. Add conversation history
        foreach (var msg in context.History)
        {
            messages.Add(msg);
        }

        // 7. Add current user message
        messages.Add(new ChatMessage { Role = ChatMessageRole.User, Content = context.UserMessage });

        return messages;
    }
}
