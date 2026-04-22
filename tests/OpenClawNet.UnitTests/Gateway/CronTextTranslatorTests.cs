using FluentAssertions;
using OpenClawNet.Gateway.Endpoints;
using Xunit;

namespace OpenClawNet.UnitTests.Gateway;

[Trait("Category", "Unit")]
public sealed class CronTextTranslatorTests
{
    [Theory]
    [InlineData("every 5 minutes", "*/5 * * * *")]
    [InlineData("every 30 minutes", "*/30 * * * *")]
    [InlineData("every minute", "* * * * *")]
    [InlineData("every hour", "0 * * * *")]
    [InlineData("hourly", "0 * * * *")]
    [InlineData("every 2 hours", "0 */2 * * *")]
    [InlineData("daily", "0 0 * * *")]
    [InlineData("every day", "0 0 * * *")]
    [InlineData("daily at 9am", "0 9 * * *")]
    [InlineData("daily at 09:30", "30 9 * * *")]
    [InlineData("every day at 5:15 pm", "15 17 * * *")]
    [InlineData("daily at noon", "0 12 * * *")]
    [InlineData("daily at midnight", "0 0 * * *")]
    [InlineData("every weekday at 9am", "0 9 * * 1-5")]
    [InlineData("weekdays at 08:30", "30 8 * * 1-5")]
    [InlineData("weekends at 10am", "0 10 * * 0,6")]
    [InlineData("every monday at 9am", "0 9 * * 1")]
    [InlineData("every friday at 17:00", "0 17 * * 5")]
    [InlineData("every sunday", "0 0 * * 0")]
    [InlineData("at 6:00", "0 6 * * *")]
    [InlineData("EVERY WEEKDAY AT 9 AM", "0 9 * * 1-5")]  // case-insensitive
    public void TryTranslate_KnownPatterns_ReturnsExpectedCron(string text, string expectedCron)
    {
        var ok = CronTextTranslator.TryTranslate(text, out var cron, out var explanation);

        ok.Should().BeTrue($"because '{text}' is a recognised pattern");
        cron.Should().Be(expectedCron);
        explanation.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("on the third tuesday of every month")]   // unsupported
    [InlineData("every fortnight")]                        // unsupported
    [InlineData("every 99 minutes")]                       // out of range
    [InlineData("daily at 25:00")]                         // invalid time
    [InlineData("every banana")]                           // garbage
    public void TryTranslate_UnsupportedOrEmpty_ReturnsFalse(string? text)
    {
        var ok = CronTextTranslator.TryTranslate(text ?? string.Empty, out var cron, out var explanation);

        ok.Should().BeFalse();
        cron.Should().BeEmpty();
        explanation.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0 9 * * 1-5", true)]
    [InlineData("*/30 * * * * *", true)]   // 6-field with seconds
    [InlineData("0 9 * *", false)]         // only 4 fields
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("0 9 * * 1-5 * *", false)] // 7 fields
    public void IsValidCron_DetectsFieldCount(string? cron, bool expected)
    {
        CronTextTranslator.IsValidCron(cron).Should().Be(expected);
    }

    [Fact]
    public void TryParseLlmJson_ValidPayload_Succeeds()
    {
        var raw = """{"cron":"0 9 * * 1-5","explanation":"Every weekday at 09:00 UTC"}""";

        var ok = CronTextTranslator.TryParseLlmJson(raw, out var cron, out var explanation, out var error);

        ok.Should().BeTrue();
        cron.Should().Be("0 9 * * 1-5");
        explanation.Should().Be("Every weekday at 09:00 UTC");
        error.Should().BeNull();
    }

    [Fact]
    public void TryParseLlmJson_TolerantOfSurroundingProse()
    {
        var raw = "Sure! Here you go:\n```json\n{\"cron\":\"0 0 * * *\",\"explanation\":\"Every midnight\"}\n```";

        var ok = CronTextTranslator.TryParseLlmJson(raw, out var cron, out _, out _);

        ok.Should().BeTrue();
        cron.Should().Be("0 0 * * *");
    }

    [Fact]
    public void TryParseLlmJson_ErrorPayload_ReturnsFalseWithMessage()
    {
        var raw = """{"error":"too ambiguous"}""";

        var ok = CronTextTranslator.TryParseLlmJson(raw, out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Be("too ambiguous");
    }

    [Fact]
    public void TryParseLlmJson_InvalidCron_RejectsPayload()
    {
        var raw = """{"cron":"not a cron","explanation":"x"}""";

        var ok = CronTextTranslator.TryParseLlmJson(raw, out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("invalid cron");
    }

    [Fact]
    public void TryParseLlmJson_NoJsonObject_ReturnsFalse()
    {
        var ok = CronTextTranslator.TryParseLlmJson("just plain text", out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }
}
