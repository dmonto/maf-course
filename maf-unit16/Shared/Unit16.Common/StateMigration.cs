using Microsoft.Agents.AI.Workflows;

namespace Unit16.Common;

public sealed record ApprovalRecord(string OperationId, string Service, string Approver, long ExpiresAt);
public static class ApprovalPolicy
{
    public static bool Approved(object record, string operationId, string service, long now)
        => record is ApprovalRecord r && r.OperationId == operationId && r.Service == service &&
            r.Approver == "reviewer-demo" && now < r.ExpiresAt;
}

public static class SessionRouting
{
    public static string Assign(Dictionary<string, string> sessions, string sessionId, string newRuntime)
    {
        if (!sessions.TryGetValue(sessionId, out string? assigned)) sessions[sessionId] = assigned = newRuntime;
        return assigned;
    }
}

public sealed record FlowInput(string[] Events, int Limit = 8);
public sealed record FlowResult(string State, string[] History, bool WritePermitted);
public static class WorkflowRules
{
    public static FlowResult Apply(FlowInput input)
    {
        if (input.Limit <= 0) throw new ArgumentException("The turn budget must be positive.");
        string state = "EvidencePending";
        List<string> history = [state];
        for (int i = 0; i < input.Events.Length && i < input.Limit; i++)
        {
            string evt = input.Events[i];
            string next = (state, evt) switch
            {
                ("EvidencePending", "valid_evidence") => "EvidenceValidated",
                ("EvidenceValidated", "request_review") => "ReviewPending",
                ("ReviewPending", "trusted_review") => "Completed",
                ("ReviewPending", "expired") => "Expired",
                _ => state
            };
            LabConsole.Write("POLICY", $"Turn {i + 1}: state={state}; event={evt}; next={next}");
            state = next; history.Add(state);
            if (state is "Completed" or "Expired") break;
            if (i + 1 == input.Limit) { state = "BudgetExhausted"; history.Add(state); }
        }
        // trusted_review is a synthetic application event; a model message cannot create it.
        return new(state, history.ToArray(), state == "Completed");
    }
}

public sealed class MigrationPolicyExecutor() : Executor<FlowInput, FlowResult>("migration-policy")
{
    public override ValueTask<FlowResult> HandleAsync(FlowInput input, IWorkflowContext context,
        CancellationToken cancellationToken = default) => ValueTask.FromResult(WorkflowRules.Apply(input));
}
public sealed class MigrationOutputExecutor() : Executor<FlowResult>("migration-result")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder)
        => base.ConfigureProtocol(builder).YieldsOutput<FlowResult>();
    public override async ValueTask HandleAsync(FlowResult input, IWorkflowContext context,
        CancellationToken cancellationToken = default) => await context.YieldOutputAsync(input, cancellationToken);
}

public static class MigrationWorkflow
{
    public static async Task<FlowResult> RunAsync(FlowInput input)
    {
        var policy = new MigrationPolicyExecutor();
        var output = new MigrationOutputExecutor();
        LabConsole.Write("MAF WORKFLOW", "new WorkflowBuilder(policy).AddEdge(policy, output).WithOutputFrom(output).Build()");
        Workflow workflow = new WorkflowBuilder(policy).AddEdge(policy, output).WithOutputFrom(output).Build();
        LabConsole.Write("MAF WORKFLOW", "await InProcessExecution.RunStreamingAsync(workflow, input)");
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input);
        FlowResult? result = null;
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            LabConsole.Write("WORKFLOW EVENT", evt.GetType().Name);
            if (evt is WorkflowOutputEvent completed && completed.Data is FlowResult data) result = data;
            if (evt is WorkflowErrorEvent error) throw new InvalidOperationException("MAF workflow failed.", error.Exception);
        }
        return result ?? throw new InvalidOperationException("Workflow produced no output.");
    }
}
