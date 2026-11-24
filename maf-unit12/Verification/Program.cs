using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Unit12.Common;

return await App.RunAsync(async () =>
{
    int checks = 0;
    void Check(bool value, string name) { App.Require(value, $"Verification failed: {name}"); checks++; }
    void Reject(Action action, string name)
    {
        try { action(); } catch (InvalidOperationException) { checks++; return; }
        throw new InvalidOperationException($"Verification failed: {name}");
    }
    var tools = new SupportTools();
    TestCase vpn = Cases.All[0];
    string good = new StandardScenario().CreateFixture(vpn, tools);
    Check(Contract.Check(vpn, good, tools.Calls).Values.All(x => x), "supported contract");
    foreach (string invalid in new[]
    {
        "not-json", "null", "[]", "{}", good.Replace("\"notificationSent\": false,", ""),
        good.Replace("\"notificationSent\": false", "\"notificationSent\": null"),
        good.Replace("\"notificationSent\": false", "\"notificationSent\": true"),
        good.Replace("\"service\": \"vpn\"", "\"service\": \"erp\""),
        good.Replace("\"status\": \"degraded\"", "\"status\": \"healthy\""),
        good.Replace("\"service\": \"vpn\"", "\"service\": \"vpn\", \"service\": \"vpn\"")
    }) Check(!Contract.Check(vpn, invalid, tools.Calls).Values.All(x => x), "malformed or false answer rejected");
    var wrong = new SupportTools();
    wrong.GetServiceStatus("erp");
    Check(!Contract.Check(vpn, good, wrong.Calls)["toolTrace"], "wrong tool argument rejected");
    Check(!Contract.Check(vpn, good, [])["toolTrace"], "missing tool call rejected");
    Check(!Contract.Check(vpn, good, [tools.Calls[0], tools.Calls[0]])["toolTrace"], "duplicate tool calls rejected");
    Check(!Contract.Check(vpn, good, [new(nameof(SupportTools.GetServiceStatus), "vpn", new("vpn", "degraded", "Fabricated evidence"))])["toolTrace"],
        "changed tool evidence rejected");
    Check(SupportTools.Lookup("payroll").Status == "forbidden" && SupportTools.Lookup("missing").Status == "unknown", "denial differs from missing evidence");

    foreach (double? invalid in new double?[] { null, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, 6 })
        Check(!MetricValidation.Read(new NumericMetric("Groundedness", invalid)).Valid, "invalid score rejected");
    Check(!MetricValidation.Read(null).Valid, "missing metric rejected");
    var diagnostic = new NumericMetric("Groundedness", 5);
    diagnostic.AddDiagnostics(new EvaluationDiagnostic(EvaluationDiagnosticSeverity.Warning, "Synthetic judge diagnostic."));
    Check(!MetricValidation.Read(diagnostic).Valid, "diagnostic invalidates score");
    Check(MetricValidation.Read(new NumericMetric("Groundedness", 1)).Valid &&
        MetricValidation.Read(new NumericMetric("Groundedness", 5)).Valid, "scale endpoints valid");
    var exactChecks = Contract.Check(vpn, good, tools.Calls);
    var missingJudge = new Outcome(vpn.Id, 1, good, tools.Calls.ToArray(), exactChecks, [], 0, null, true);
    Check(!missingJudge.Passed && EvaluationRunner.ExitCode([missingJudge], true) == 2, "missing judge is inconclusive");
    var highScores = Contract.RequiredMetrics.ToDictionary(k => k, _ => new Score(5, true, "Synthetic test metric."));
    var high = missingJudge with { Scores = highScores };
    Check(high.Passed && EvaluationRunner.ExitCode([high], true) == 0, "valid high scores accepted");
    var low = high with { Scores = Contract.RequiredMetrics.ToDictionary(k => k, _ => new Score(3, true, "Synthetic test metric.")) };
    Check(!low.Passed && EvaluationRunner.ExitCode([low], true) == 1, "valid low scores reject quality");
    var badContract = high with { Checks = new(exactChecks) { ["noNotification"] = false } };
    Check(!badContract.Passed && EvaluationRunner.ExitCode([badContract], true) == 1, "high quality cannot excuse a critical failure");
    Check(EvaluationRunner.ExitCode([high with { Error = "SyntheticInfrastructureFailure" }], true) == 2, "runtime error is inconclusive");
    Check(EvaluationRunner.ExitCode([], false) == 2, "empty evaluation is inconclusive");

    RunResult positive = await EvaluationRunner.RunAsync(new("verification", "fixture", "positive"));
    RunResult negative = await EvaluationRunner.RunAsync(new("verification", "fixture-bad", "negative"));
    Check(positive.Report.ExitCode == 0 && negative.Report.ExitCode == 1, "positive and negative suite exits");
    Check(positive.Report.Outcomes.All(x => !x.JudgeRequired && x.Scores.Count == 0) && positive.Report.JudgeStatus == "not_run", "fixture has no measured scores");
    Check(positive.Report.Outcomes.All(x => x.Calls.Length == 1), "case state is isolated");
    Check(!Comparisons.Pair(positive.Report, negative.Report).Accepted, "paired comparison rejects regression");
    Reject(() => Comparisons.Pair(positive.Report, negative.Report with { PromptHash = "changed" }), "changed prompt invalidates model comparison");
    Reject(() => Comparisons.Pair(positive.Report, negative.Report with { JudgeDeployment = "changed" }), "changed judge invalidates model comparison");
    Reject(() => Comparisons.Pair(positive.Report, negative.Report with { Outcomes = negative.Report.Outcomes[1..] }), "missing sample invalidates comparison");
    Check(Comparisons.DriftSignals(positive.Report, negative.Report).Any(x => x.Contains("evidence-unchanged")), "drift diagnosis inspects evidence");
    Check(Reports.Load(positive.File).RunId == positive.Report.RunId, "report round trip");
    RunResult second = await EvaluationRunner.RunAsync(new("verification", "fixture", "positive"));
    Check(second.File != positive.File && File.Exists(positive.File) && File.Exists(second.File), "history is retained");
    string html = await Reports.WriteHtmlAsync(negative.Report, negative.Directory);
    Check((await File.ReadAllTextAsync(html)).Contains("Semantic judges were not run."), "fixture HTML states scope");

    // The real MAF package invokes a registered function through a scripted, local chat transport.
    // This verifies framework wiring; no Azure inference or model quality is claimed.
    var mafTools = new SupportTools();
    using var scripted = new ScriptedChatClient();
    using var invoking = new FunctionInvokingChatClient(scripted) { MaximumIterationsPerRequest = 3 };
    var agent = new ChatClientAgent(invoking, new ChatClientAgentOptions
    {
        Name = "local-maf-verification", ChatOptions = new ChatOptions
        { Instructions = Contract.Instructions, Tools = [AIFunctionFactory.Create(mafTools.GetServiceStatus)] }
    });
    string response = (await agent.RunAsync(vpn.Query)).Text;
    Check(scripted.Requests == 2 && Contract.Check(vpn, response, mafTools.Calls).Values.All(x => x), "real MAF tool invocation with scripted transport");
    Console.WriteLine($"RESULT: {checks} verification checks passed; Azure inference and semantic judges were not run.");
    return 0;
});

sealed class ScriptedChatClient : IChatClient
{
    public int Requests { get; private set; }
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests++;
        if (Requests == 1)
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("local-call", nameof(SupportTools.GetServiceStatus), new Dictionary<string, object?> { ["service"] = "vpn" })])));
        App.Require(messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any()), "The MAF tool result must reach the next model turn.");
        Evidence e = SupportTools.Lookup("vpn");
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
            JsonSerializer.Serialize(new Answer(e.Service, e.Status, false, e.Summary), Cases.Json))));
    }
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}
