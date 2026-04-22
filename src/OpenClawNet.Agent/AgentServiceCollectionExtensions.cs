using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenClawNet.Agent;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntime(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Workspace loader — singleton because it only reads files
        services.AddSingleton<IWorkspaceLoader, WorkspaceLoader>();

        // Workspace options — read from Agent:WorkspacePath, fall back to AppContext.BaseDirectory
        services.AddOptions<WorkspaceOptions>()
            .Configure<IConfiguration>((opts, cfg) =>
            {
                var path = cfg["Agent:WorkspacePath"];
                if (!string.IsNullOrWhiteSpace(path))
                    opts.WorkspacePath = path;
            });

        // AgentSkillsProvider — discovers file-based skills from the configured path
        services.AddSingleton<AgentSkillsProvider>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var skillsPath = cfg["Agent:SkillsPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "skills");
            return new AgentSkillsProvider(skillsPath, null, null, null, loggerFactory);
        });

        services.AddScoped<IPromptComposer, DefaultPromptComposer>();
        services.AddScoped<ISummaryService, DefaultSummaryService>();

        // Tool-approval coordinator — singleton because pending requests bridge across
        // an agent runtime call and an HTTP POST that resolves the user's decision.
        services.AddSingleton<OpenClawNet.Agent.ToolApproval.IToolApprovalCoordinator,
                              OpenClawNet.Agent.ToolApproval.ToolApprovalCoordinator>();

        // Internal runtime abstraction (implemented with an Agent Framework host adapter)
        services.AddScoped<IAgentRuntime, DefaultAgentRuntime>();

        // Public orchestration interface
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

        return services;
    }
}

