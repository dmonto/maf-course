using System.Diagnostics;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Unit08.Common;

return await ExampleHost.RunAsync(args, "v09", "combined", async context =>
{
    var results = new List<object>();
    var outcomes = new List<CaseOutcome>();
    foreach (bool serial in new[] { true, false })
    {
        var watch = Stopwatch.StartNew();
        CaseOutcome outcome = await context.Supervisor.RunAsync(context.NewCase(),
            context.Arguments.RunOptions with { Serial = serial }, context.CancellationToken);
        watch.Stop();
        outcomes.Add(outcome);
        results.Add(new { Pattern = serial ? "sequential-consultations" : "concurrent-consultations",
            ElapsedMs = watch.ElapsedMilliseconds, Outcome = outcome });
    }
    // Group chat built with MAF: AgentWorkflowBuilder.CreateGroupChatBuilderWith + round-robin manager.
    {
        var watch = Stopwatch.StartNew();
        var transcript = await RunGroupChatAsync(context);
        watch.Stop();
        results.Add(new { Pattern = "group-chat (AgentWorkflowBuilder)", ElapsedMs = watch.ElapsedMilliseconds,
            Rounds = transcript.Count, Transcript = transcript });
    }
    bool review = outcomes.Any(o => o.Stage == "needs-review");
    if (outcomes[0].Stage != outcomes[1].Stage)
        throw new InvalidOperationException("pattern-outcome-mismatch");
    return context.Result(review ? "needs-review" : "patterns-compared", new
    {
        Implementation = "application-owned C# scheduling around MAF agents",
        PatternResults = results,
        OtherPatternsDiscussedInGitBook = new[] { "handoff", "magentic" }
    }, review ? 2 : 0);
});

static async Task<List<ChatLine>> RunGroupChatAsync(ExampleContext context)
{
    var ct = context.CancellationToken;
    var evidence = Supervisor.Plan(context.NewCase("combined")).Select(t => t.Evidence).ToArray();
    string facts = string.Join(" | ", evidence.Select(e => $"{e.SourceId}: {e.Subject} is {e.Status}. {e.Detail}"));

    IChatClient Client(string role, Func<string> demoReply) => context.Settings.Live
        ? LiveChat()
        : new ScriptedChatClient(demoReply);
    AIAgent Agent(string name, string instructions, Func<string> demoReply) =>
        new ChatClientAgent(Client(name, demoReply), instructions, name, null, null, null, null);

    AIAgent[] agents =
    [
        Agent("network", $"You are the network specialist. Facts: {facts} Comment only on VPN in at most 30 words; propose collecting the VPN error code and client version.",
            () => "VPN is healthy. Proposal: collect the VPN error code and client version."),
        Agent("service", $"You are the payroll service specialist. Facts: {facts} Comment only on payroll in at most 30 words; propose referring to the service owner. Do not invent a recovery time.",
            () => "Payroll is degraded, no recovery time. Proposal: refer to the payroll service owner."),
        Agent("coordinator", "You are the coordinator. Summarize the previous messages in at most 50 words, as proposals only.",
            () => "Summary: VPN healthy, collect client details; payroll degraded, refer to the service owner (proposals only).")
    ];

    var workflow = AgentWorkflowBuilder
        .CreateGroupChatBuilderWith(participants => new RoundRobinGroupChatManager(participants) { MaximumIterationCount = agents.Length })
        .AddParticipants(agents)
        .Build();

    var messages = new List<ChatMessage> { new(ChatRole.User, "Triage this support case: " + facts) };
    await using Run run = await InProcessExecution.RunAsync(workflow, messages, cancellationToken: ct);
    var lines = new List<ChatLine>();
    foreach (WorkflowEvent evt in run.NewEvents)
        if (evt is WorkflowOutputEvent { Data: List<ChatMessage> output })
            lines.AddRange(output.Where(m => m.Role == ChatRole.Assistant)
                .Select((m, i) => new ChatLine(i + 1, m.AuthorName ?? "?", m.Text)));
    return lines;
}

static IChatClient LiveChat()
{
    var client = new AzureOpenAIClient(new Uri(DotEnv.Required("AZURE_OPENAI_ENDPOINT")), new AzureCliCredential());
    return client.GetChatClient(DotEnv.Required("AZURE_OPENAI_DEPLOYMENT_NAME")).AsIChatClient();
}

sealed record ChatLine(int Round, string Speaker, string Text);

// Deterministic stand-in model so the group-chat workflow runs offline in --demo mode.
sealed class ScriptedChatClient(Func<string> reply) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply())));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, reply());
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
