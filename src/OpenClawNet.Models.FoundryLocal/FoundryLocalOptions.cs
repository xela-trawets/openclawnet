namespace OpenClawNet.Models.FoundryLocal;

public sealed class FoundryLocalOptions
{
    public string AppName { get; set; } = "OpenClawNet";
    public string Model { get; set; } = "phi-4";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4096;
    public bool EnableWebServer { get; set; }
}
