using FluentAssertions;
using OpenClawNet.Models.Abstractions;

namespace OpenClawNet.UnitTests.Models;

public class AgentProfileTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        var profile = new AgentProfile { Name = "test" };

        profile.DisplayName.Should().BeNull();
        profile.Provider.Should().BeNull();
        profile.Instructions.Should().BeNull();
        profile.EnabledTools.Should().BeNull();
        profile.Temperature.Should().BeNull();
        profile.MaxTokens.Should().BeNull();
        profile.IsDefault.Should().BeFalse();
        profile.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        profile.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RequiredName_CanBeSetAndRetrieved()
    {
        var profile = new AgentProfile { Name = "my-agent" };

        profile.Name.Should().Be("my-agent");
    }

    [Fact]
    public void AllProperties_CanBeAssigned()
    {
        var now = DateTime.UtcNow;
        var profile = new AgentProfile
        {
            Name = "full-agent",
            DisplayName = "Full Agent",
            Provider = "ollama",
            Instructions = "Be helpful.",
            EnabledTools = "file_system,shell",
            Temperature = 0.8,
            MaxTokens = 2048,
            IsDefault = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        profile.Name.Should().Be("full-agent");
        profile.DisplayName.Should().Be("Full Agent");
        profile.Provider.Should().Be("ollama");
        profile.Instructions.Should().Be("Be helpful.");
        profile.EnabledTools.Should().Be("file_system,shell");
        profile.Temperature.Should().Be(0.8);
        profile.MaxTokens.Should().Be(2048);
        profile.IsDefault.Should().BeTrue();
        profile.CreatedAt.Should().Be(now);
        profile.UpdatedAt.Should().Be(now);
    }
}
