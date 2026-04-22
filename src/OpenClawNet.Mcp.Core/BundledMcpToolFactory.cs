using System.Reflection;
using ModelContextProtocol.Server;

namespace OpenClawNet.Mcp.Core;

/// <summary>
/// Reflection helper shared across bundled wrappers — turns <c>[McpServerTool]</c>
/// instance methods on <paramref name="target"/> into <see cref="McpServerTool"/> primitives.
/// </summary>
public static class BundledMcpToolFactory
{
    public static IReadOnlyList<McpServerTool> CreateFor(object target)
    {
        var methods = target.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

        var list = new List<McpServerTool>();
        foreach (var method in methods)
            list.Add(McpServerTool.Create(method, target));
        return list;
    }
}

