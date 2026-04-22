using OpenClawNet.Tools.Abstractions;

namespace OpenClawNet.Tools.Core;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    
    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
    }
    
    public ITool? GetTool(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;
    
    public IReadOnlyList<ITool> GetAllTools() =>
        _tools.Values.ToList();
    
    public IReadOnlyList<ToolMetadata> GetToolManifest() =>
        _tools.Values.Select(t => t.Metadata).ToList();
}
