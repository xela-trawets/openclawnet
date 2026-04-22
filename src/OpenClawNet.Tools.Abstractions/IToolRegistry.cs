namespace OpenClawNet.Tools.Abstractions;

public interface IToolRegistry
{
    void Register(ITool tool);
    ITool? GetTool(string name);
    IReadOnlyList<ITool> GetAllTools();
    IReadOnlyList<ToolMetadata> GetToolManifest();
}
