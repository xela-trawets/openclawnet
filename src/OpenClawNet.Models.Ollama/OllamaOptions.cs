namespace OpenClawNet.Models.Ollama;

/// <summary>
/// Configuration options for the Ollama model provider.
/// </summary>
/// <remarks>
/// Supported / recommended models (pull with <c>ollama pull &lt;model&gt;</c>):
/// <list type="table">
///   <item><term>gemma4:e2b</term><description>Google Gemma 4 E2B — native function calling, 128K context, edge-optimised (recommended)</description></item>
///   <item><term>llama3.2</term><description>Meta Llama 3.2 — solid general-purpose model, good tool-use support</description></item>
///   <item><term>gemma4:e4b</term><description>Google Gemma 4 E4B — more capable than E2B, still edge-friendly</description></item>
///   <item><term>gemma4:26b</term><description>Google Gemma 4 26B (MoE 4B active) — workstation-class reasoning</description></item>
///   <item><term>phi4</term><description>Microsoft Phi-4 — strong reasoning on constrained hardware</description></item>
///   <item><term>mistral</term><description>Mistral 7B — fast, compact, general-purpose</description></item>
/// </list>
/// </remarks>
public sealed class OllamaOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>Default model name. Recommended: <c>gemma4:e2b</c> or <c>llama3.2</c>.</summary>
    public string Model { get; set; } = "llama3.2";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4096;
}
