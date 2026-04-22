namespace OpenClawNet.Models.Abstractions;

public sealed class ModelOptions
{
    public string Provider { get; set; } = "ollama";
    public string Model { get; set; } = "llama3.2";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4096;
}
