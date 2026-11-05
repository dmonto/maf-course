using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace Unit07.Common;

public sealed class IntakeExecutor() : Executor<SupportRequest, SupportRequest>("intake")
{
    public override ValueTask<SupportRequest> HandleAsync(SupportRequest input,
        IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (input.CaseId != "INC-DEMO-001" || input.Service is not ("vpn" or "payroll")
            || string.IsNullOrWhiteSpace(input.Symptom) || input.Symptom.Length > 500)
            throw new ArgumentException("invalid-request");
        return ValueTask.FromResult(input);
    }
}

public sealed class EvidenceExecutor(RuntimeOptions options)
    : Executor<SupportRequest, Evidence>("evidence")
{
    public override async ValueTask<Evidence> HandleAsync(SupportRequest input,
        IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // These two reads are independent and receive the same validated request.
        var timer = System.Diagnostics.Stopwatch.StartNew();
        string statusValue, guideValue;
        if (options.Arguments.Has("--serial"))
        {
            statusValue = await ReadStatusAsync(input.Service, cancellationToken);
            guideValue = await ReadGuideAsync(input.Service, cancellationToken);
        }
        else
        {
            Task<string> status = ReadStatusAsync(input.Service, cancellationToken);
            Task<string> guide = ReadGuideAsync(input.Service, cancellationToken);
            await Task.WhenAll(status, guide);
            statusValue = await status;
            guideValue = await guide;
        }
        JsonOutput.Write(new { kind = "evidence-timing", serial = options.Arguments.Has("--serial"),
            elapsedMs = timer.ElapsedMilliseconds });
        return new(input, statusValue, guideValue);
    }
    private async Task<string> ReadStatusAsync(string service, CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                JsonOutput.Write(new { kind = "status-attempt", attempt });
                await Task.Delay(options.StatusDelayMs, ct);
                if (options.Arguments.Has("--fail")) throw new TimeoutException("status-timeout");
                return service == "payroll" ? "degraded" : "healthy";
            }
            catch (TimeoutException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
            }
        }
    }
    private async Task<string> ReadGuideAsync(string service, CancellationToken ct)
    {
        await Task.Delay(options.GuideDelayMs, ct);
        return service == "vpn"
            ? "Collect the VPN error code and client version. Do not reset credentials."
            : "Record the payroll incident and refer it to the service owner. Do not modify payroll data.";
    }
}

public sealed class DraftExecutor(AIAgent? agent, int timeoutSeconds = 45) : Executor<Evidence, Proposal>("draft")
{
    public override async ValueTask<Proposal> HandleAsync(Evidence input,
        IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        string text;
        if (agent is null)
            text = $"Service {input.Request.Service}: {input.Status}. {input.Guide}";
        else
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            var response = await agent.RunAsync(JsonSerializer.Serialize(input),
                cancellationToken: deadline.Token);
            text = response.Text;
        }
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2000)
            throw new InvalidOperationException("invalid-draft");
        // The model cannot choose or bypass this business rule.
        var proposal = new Proposal(input, text, input.Status == "degraded");
        await context.QueueStateUpdateAsync(LabState.ProposalKey, proposal,
            scopeName: LabState.Scope, cancellationToken: cancellationToken);
        return proposal;
    }
}

public sealed class AutoFinishExecutor() : Executor<Proposal>("finish-information")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
        => base.ConfigureProtocol(builder).YieldsOutput<Outcome>();
    public override async ValueTask HandleAsync(Proposal input, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (input.NeedsApproval) throw new InvalidOperationException("approval-required");
        await context.YieldOutputAsync(new Outcome(input.Evidence.Request.CaseId,
            "information-ready", input.Evidence.Status, input.Draft), cancellationToken);
    }
}

public sealed class ReviewedFinishExecutor() : Executor<bool>("finish-reviewed")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
        => base.ConfigureProtocol(builder).YieldsOutput<Outcome>();
    public override async ValueTask HandleAsync(bool approved, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var proposal = await context.ReadStateAsync<Proposal>(LabState.ProposalKey,
            scopeName: LabState.Scope, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("missing-proposal");
        if (!proposal.NeedsApproval) throw new InvalidOperationException("unexpected-review");
        await context.YieldOutputAsync(new Outcome(proposal.Evidence.Request.CaseId,
            approved ? "escalation-approved" : "escalation-rejected",
            proposal.Evidence.Status, proposal.Draft), cancellationToken);
    }
}

public static class WorkflowFactory
{
    public static Workflow Build(AIAgent? agent, RuntimeOptions options)
    {
        var intake = new IntakeExecutor();
        var evidence = new EvidenceExecutor(options);
        var draft = new DraftExecutor(agent, options.TimeoutSeconds);
        var review = RequestPort.Create<Proposal, bool>("human-review");
        var autoFinish = new AutoFinishExecutor();
        var reviewedFinish = new ReviewedFinishExecutor();
        return new WorkflowBuilder(intake)
            .AddEdge(intake, evidence)
            .AddEdge(evidence, draft)
            .AddEdge<Proposal>(draft, review, p => p is { NeedsApproval: true })
            .AddEdge<Proposal>(draft, autoFinish, p => p is { NeedsApproval: false })
            .AddEdge(review, reviewedFinish)
            .WithOutputFrom(autoFinish, reviewedFinish)
            .Build();
    }
}
