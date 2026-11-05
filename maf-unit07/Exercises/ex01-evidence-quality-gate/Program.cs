using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Unit07.Common;

return await WorkflowHost.RunAsync(args, new("ex01"), ExerciseWorkflow.Build, ["--stale"]);

public sealed record TimedEvidence(Evidence Evidence, DateTimeOffset ObservedUtc);

// The synthetic source reports when its observation was made.
public sealed class TimestampExecutor(bool stale) : Executor<Evidence, TimedEvidence>("timestamp-evidence")
{
    public override ValueTask<TimedEvidence> HandleAsync(Evidence input, IWorkflowContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new TimedEvidence(input,
            DateTimeOffset.UtcNow.AddMinutes(stale ? -30 : 0)));
}

public sealed class EvidenceQualityExecutor() : Executor<TimedEvidence, Evidence>("evidence-quality-gate")
{
    public override ValueTask<Evidence> HandleAsync(TimedEvidence input, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var age = DateTimeOffset.UtcNow - input.ObservedUtc;
        if (age > TimeSpan.FromMinutes(5) || age < TimeSpan.Zero)
            throw new InvalidOperationException("stale-or-future-evidence");
        JsonOutput.Write(new { kind = "evidence-quality", accepted = true, observedUtc = input.ObservedUtc });
        return ValueTask.FromResult(input.Evidence);
    }
}

public static class ExerciseWorkflow
{
    public static Workflow Build(AIAgent? agent, RuntimeOptions options)
    {
        var intake = new IntakeExecutor();
        var evidence = new EvidenceExecutor(options);
        var timestamp = new TimestampExecutor(options.Arguments.Has("--stale"));
        var gate = new EvidenceQualityExecutor();
        var draft = new DraftExecutor(agent, options.TimeoutSeconds);
        var review = RequestPort.Create<Proposal, bool>("human-review");
        var autoFinish = new AutoFinishExecutor();
        var reviewedFinish = new ReviewedFinishExecutor();
        return new WorkflowBuilder(intake)
            .AddEdge(intake, evidence).AddEdge(evidence, timestamp)
            .AddEdge(timestamp, gate).AddEdge(gate, draft)
            .AddEdge<Proposal>(draft, review, p => p is { NeedsApproval: true })
            .AddEdge<Proposal>(draft, autoFinish, p => p is { NeedsApproval: false })
            .AddEdge(review, reviewedFinish)
            .WithOutputFrom(autoFinish, reviewedFinish).Build();
    }
}
