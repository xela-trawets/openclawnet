using FluentAssertions;
using OpenClawNet.Services.Scheduler.Services;

namespace OpenClawNet.UnitTests.Scheduler;

public sealed class SchedulerOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var opts = new SchedulerOptions();
        opts.PollIntervalSeconds.Should().Be(30);
        opts.Enabled.Should().BeTrue();
        opts.MaxConcurrentJobs.Should().Be(3);
        opts.JobTimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public void Validate_ClampsPollIntervalSeconds_ToMinimum()
    {
        var opts = new SchedulerOptions { PollIntervalSeconds = 1 };
        opts.Validate();
        opts.PollIntervalSeconds.Should().Be(5);
    }

    [Fact]
    public void Validate_ClampsPollIntervalSeconds_ToMaximum()
    {
        var opts = new SchedulerOptions { PollIntervalSeconds = 10000 };
        opts.Validate();
        opts.PollIntervalSeconds.Should().Be(3600);
    }

    [Fact]
    public void Validate_ClampsMaxConcurrentJobs_ToMinimum()
    {
        var opts = new SchedulerOptions { MaxConcurrentJobs = 0 };
        opts.Validate();
        opts.MaxConcurrentJobs.Should().Be(1);
    }

    [Fact]
    public void Validate_ClampsMaxConcurrentJobs_ToMaximum()
    {
        var opts = new SchedulerOptions { MaxConcurrentJobs = 100 };
        opts.Validate();
        opts.MaxConcurrentJobs.Should().Be(20);
    }

    [Fact]
    public void Validate_ClampsJobTimeoutSeconds_ToMinimum()
    {
        var opts = new SchedulerOptions { JobTimeoutSeconds = 1 };
        opts.Validate();
        opts.JobTimeoutSeconds.Should().Be(10);
    }

    [Fact]
    public void Validate_ClampsJobTimeoutSeconds_ToMaximum()
    {
        var opts = new SchedulerOptions { JobTimeoutSeconds = 10000 };
        opts.Validate();
        opts.JobTimeoutSeconds.Should().Be(7200);
    }

    [Fact]
    public void Validate_PreservesValidValues()
    {
        var opts = new SchedulerOptions
        {
            PollIntervalSeconds = 60,
            MaxConcurrentJobs = 5,
            JobTimeoutSeconds = 600
        };
        opts.Validate();
        opts.PollIntervalSeconds.Should().Be(60);
        opts.MaxConcurrentJobs.Should().Be(5);
        opts.JobTimeoutSeconds.Should().Be(600);
    }
}
