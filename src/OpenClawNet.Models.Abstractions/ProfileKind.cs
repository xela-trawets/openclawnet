using System.Text.Json.Serialization;

namespace OpenClawNet.Models.Abstractions;

/// <summary>
/// Discriminator for <see cref="AgentProfile"/> that controls where a profile
/// can be selected in the system.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>Standard</c> — usable in chat sessions, jobs, and channels. Default.</item>
///   <item><c>System</c> — used for internal platform tasks (e.g. natural-language → cron
///       translation, prompt rewriting). Hidden from chat pickers.</item>
///   <item><c>ToolTester</c> — only invoked from the Tool Test surface to validate that
///       an LLM can interpret a tool's schema. Hidden from chat pickers and job pickers.</item>
/// </list>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ProfileKind>))]
public enum ProfileKind
{
    Standard = 0,
    System = 1,
    ToolTester = 2,
}
