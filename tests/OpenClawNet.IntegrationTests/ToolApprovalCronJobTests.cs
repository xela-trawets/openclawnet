namespace OpenClawNet.IntegrationTests;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Wave 4 PR-3 — Integration scaffolding for the unattended-approval guardrail.
///
/// Bruno Q3 + Ripley's recommendation (docs/analysis/tool-approval-design.md,
/// Question 3 / Option D2): a cron-triggered job whose AgentProfile has
/// <c>RequireToolApproval = true</c> must FAIL FAST inside the JobExecutor —
/// since there is no human present to approve, allowing the run would be a
/// silent escalation of privilege.
///
/// This test is <see cref="FactAttribute.Skip"/>-marked until:
///   • PR-2 (Dallas) lands <c>AgentProfile.RequireToolApproval</c> +
///     the <c>JobExecutor</c> guard that refuses to start such jobs (or
///     fails the run with a clear error / <c>JobRun.ApprovalDenied</c> reason).
///
/// Tagged <c>Trait("Category", "Integration")</c> following the existing scheme.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ToolApprovalCronJobTests(GatewayWebAppFactory factory)
    : IClassFixture<GatewayWebAppFactory>
{
    [Fact]
    public async Task CronJob_WithRequiringProfile_FailsFast()
    {
        // Arrange — resolve the gateway's services and seed an AgentProfile that requires
        // approval, plus a cron-triggered ScheduledJob that targets that profile.
        var scope = factory.Services.CreateScope();
        var profileStore = scope.ServiceProvider.GetRequiredService<OpenClawNet.Storage.IAgentProfileStore>();
        var dbFactory = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<OpenClawNet.Storage.OpenClawDbContext>>();
        var executor = scope.ServiceProvider.GetRequiredService<OpenClawNet.Gateway.Services.JobExecutor>();

        var profileName = $"approval-required-{Guid.NewGuid():N}";
        await profileStore.SaveAsync(new OpenClawNet.Models.Abstractions.AgentProfile
        {
            Name = profileName,
            Provider = "ollama",
            Instructions = "test",
            RequireToolApproval = true
        });

        await using var db = await dbFactory.CreateDbContextAsync();
        var job = new OpenClawNet.Storage.Entities.ScheduledJob
        {
            Name = "nightly-test",
            Prompt = "do work",
            CronExpression = "0 9 * * *",
            IsRecurring = true,
            Status = OpenClawNet.Storage.Entities.JobStatus.Active,
            AgentProfileName = profileName
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        // Act — invoke the executor directly; this is the same path JobSchedulerService uses.
        var result = await executor.ExecuteJobAsync(job.Id);

        // Assert — fail-fast with a clear, human-readable error. No exception escaped.
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        result.Error.Should().Contain("approval", "the failure message must explain WHY the job was refused");

        // And — the JobRun was persisted so the operator can see what happened.
        await using var verifyDb = await dbFactory.CreateDbContextAsync();
        var runs = await verifyDb.JobRuns.Where(r => r.JobId == job.Id).ToListAsync();
        runs.Should().ContainSingle();
        runs[0].Status.Should().Be("failed");
        runs[0].Error.Should().Contain("approval");
    }
}
