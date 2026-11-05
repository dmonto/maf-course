using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Unit07.Common;

return await WorkflowHost.RunAsync(args, new("ex02", "payroll", KeepFailedBookmark: true),
    ExerciseWorkflow.Build, ["--fail-after-save"]);

public sealed record ApprovedEscalation(Proposal Proposal);
public sealed record EscalationOutcome(string CaseId, string Stage, string Status,
    string? EscalationId, string? OperationKey, bool Reused);

public sealed class ReviewDecisionExecutor() : Executor<bool>("review-decision")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
        => base.ConfigureProtocol(builder).SendsMessage<ApprovedEscalation>().YieldsOutput<EscalationOutcome>();
    public override async ValueTask HandleAsync(bool approved,
        IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var proposal = await context.ReadStateAsync<Proposal>(LabState.ProposalKey,
            scopeName: LabState.Scope, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("missing-proposal");
        if (!proposal.NeedsApproval) throw new InvalidOperationException("unexpected-review");
        if (!approved)
        {
            await context.YieldOutputAsync(new EscalationOutcome(proposal.Evidence.Request.CaseId,
                "escalation-rejected", proposal.Evidence.Status, null, null, false), cancellationToken);
            return;
        }
        await context.SendMessageAsync(new ApprovedEscalation(proposal), cancellationToken: cancellationToken);
    }
}

public sealed class WriteEscalationExecutor(EscalationRepository repository, bool failAfterSave)
    : Executor<ApprovedEscalation>("write-escalation")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
        => base.ConfigureProtocol(builder).YieldsOutput<EscalationOutcome>();
    public override async ValueTask HandleAsync(ApprovedEscalation input, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var result = repository.GetOrCreate(input.Proposal);
        JsonOutput.Write(new { kind = "operation-receipt", result.EscalationId, result.OperationKey, result.Reused,
            count = repository.Count() });
        // The database commit already happened. Replaying must reuse the same receipt.
        if (failAfterSave) throw new InvalidOperationException("injected-failure-after-save");
        await context.YieldOutputAsync(result, cancellationToken);
    }
}

public static class ExerciseWorkflow
{
    public static Workflow Build(AIAgent? agent, RuntimeOptions options)
    {
        var intake = new IntakeExecutor();
        var evidence = new EvidenceExecutor(options);
        var draft = new DraftExecutor(agent, options.TimeoutSeconds);
        var review = RequestPort.Create<Proposal, bool>("human-review");
        var autoFinish = new AutoFinishExecutor();
        var decision = new ReviewDecisionExecutor();
        var repository = new EscalationRepository(Path.Combine(options.DataDirectory, "escalations.db"));
        var write = new WriteEscalationExecutor(repository, options.Arguments.Has("--fail-after-save"));
        return new WorkflowBuilder(intake)
            .AddEdge(intake, evidence).AddEdge(evidence, draft)
            .AddEdge<Proposal>(draft, review, p => p is { NeedsApproval: true })
            .AddEdge<Proposal>(draft, autoFinish, p => p is { NeedsApproval: false })
            .AddEdge(review, decision).AddEdge(decision, write)
            .WithOutputFrom(autoFinish, decision, write).Build();
    }
}
