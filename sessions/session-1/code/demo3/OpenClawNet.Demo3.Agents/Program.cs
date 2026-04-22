using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClawNet.Models.Abstractions;
using OpenClawNet.Models.Ollama;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

// ──────────────────────────────────────────────
// Demo 3 (Plan B): Agent Personality Switching
// ──────────────────────────────────────────────
// Same OllamaAgentProvider, three different personalities.
// Each AgentProfile has unique Instructions that shape behavior.

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.Configure<OllamaOptions>(o =>
{
    o.Endpoint = "http://localhost:11434";
    o.Model = "gemma4:e2b";
});
services.AddSingleton<IAgentProvider, OllamaAgentProvider>();

using var sp = services.BuildServiceProvider();
var provider = sp.GetRequiredService<IAgentProvider>();

Console.WriteLine("🦀 OpenClaw .NET — Demo 3: Agent Personalities");
Console.WriteLine($"   Provider: {provider.ProviderName}");
Console.WriteLine();

if (!await provider.IsAvailableAsync())
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("❌ Ollama is not available. Run: ollama serve");
    Console.ResetColor();
    return;
}

// Define three agent personalities
var agents = new[]
{
    new AgentProfile
    {
        Name = "captain-claw",
        DisplayName = "🏴‍☠️ Captain Claw",
        Instructions = """
            You are Captain Claw, a salty .NET pirate who loves code and the open seas.
            Talk like a pirate — use "Arr!", "Ahoy!", nautical metaphors.
            But always provide real, accurate technical answers.
            Keep answers to 2-3 sentences.
            """
    },
    new AgentProfile
    {
        Name = "chef-byte",
        DisplayName = "👨‍🍳 Chef Byte",
        Instructions = """
            You are Chef Byte, a cooking-themed software developer.
            Explain everything using cooking metaphors — dependencies are ingredients,
            builds are recipes, deployments are plating.
            Keep answers to 2-3 sentences.
            """
    },
    new AgentProfile
    {
        Name = "robochat",
        DisplayName = "🤖 RoboChat",
        Instructions = """
            You are RoboChat, a formal and precise robot assistant.
            Use structured responses. Be efficient and exact.
            Prefer bullet points. No humor. Maximum information density.
            Keep answers to 2-3 sentences.
            """
    }
};

var question = "What is Aspire and why should I use it?";

Console.ForegroundColor = ConsoleColor.Cyan;
Console.Write("Question: ");
Console.ResetColor();
Console.WriteLine(question);
Console.WriteLine(new string('─', 60));

foreach (var agent in agents)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  {agent.DisplayName}");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"  Profile: {agent.Name}");
    Console.ResetColor();
    Console.Write("  ");

    IChatClient chatClient = provider.CreateChatClient(agent);

    // Build messages with system instructions + user question
    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, agent.Instructions),
        new(ChatRole.User, question)
    };

    await foreach (var update in chatClient.GetStreamingResponseAsync(messages))
    {
        Console.Write(update.Text);
    }

    Console.WriteLine();
    Console.WriteLine(new string('─', 60));
}

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("✅ Same provider, same model, three different personalities!");
Console.ResetColor();
