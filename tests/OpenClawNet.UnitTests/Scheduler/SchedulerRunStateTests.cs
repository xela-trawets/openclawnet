using FluentAssertions;
using OpenClawNet.Services.Scheduler.Services;

namespace OpenClawNet.UnitTests.Scheduler;

public sealed class SchedulerRunStateTests
{
    [Fact]
    public void InitialCount_IsZero()
    {
        var state = new SchedulerRunState();
        state.RunningJobCount.Should().Be(0);
    }

    [Fact]
    public void Increment_IncreasesCount()
    {
        var state = new SchedulerRunState();
        state.Increment();
        state.RunningJobCount.Should().Be(1);
    }

    [Fact]
    public void Decrement_DecreasesCount()
    {
        var state = new SchedulerRunState();
        state.Increment();
        state.Increment();
        state.Decrement();
        state.RunningJobCount.Should().Be(1);
    }

    [Fact]
    public async Task IsThreadSafe_UnderConcurrentIncrementDecrement()
    {
        var state = new SchedulerRunState();
        const int n = 1000;

        var inc = Enumerable.Range(0, n).Select(_ => Task.Run(state.Increment));
        var dec = Enumerable.Range(0, n).Select(_ => Task.Run(state.Decrement));

        await Task.WhenAll(inc.Concat(dec));

        state.RunningJobCount.Should().Be(0);
    }
}
