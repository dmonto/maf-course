using System.Diagnostics;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace Unit12.Common;

public sealed record ModelIdentity(string? Name, string? Version);
public sealed record RunSpec(string ExampleId, string Mode = "fixture", string? Label = null,
    string? AgentDeployment = null, IScenarioVariant? Variant = null, ModelIdentity? AgentIdentity = null);

public sealed record RunReport(string RunId, string Mode, string VariantId, string DatasetVersion,
    int Repetitions, string PromptHash, string DatasetHash, string FixtureHash, string AgentDeployment,
    string? AgentModel, string? AgentModelVersion, string JudgeDeployment, string? JudgeModel,
    string? JudgeModelVersion, string JudgeStatus, string EvaluationPackageVersion, string MafPackageVersion,
    int MinimumQualityScore, string? Commit, DateTimeOffset ExecutedAtUtc, int ExitCode, Outcome[] Outcomes);

public sealed record RunResult(RunReport Report, string File, string Directory);

public static class EvaluationRunner
{
    public static async Task<RunResult> RunAsync(RunSpec spec)
    {
        if (spec.Mode is not ("fixture" or "fixture-bad" or "azure")) throw new ArgumentException("Unknown runner mode.");
        bool live = spec.Mode == "azure";
        int repetitions = UnitEnvironment.Repetitions();
        string runId = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";
        string root = Reports.ArtifactDirectory(spec.ExampleId, spec.Label);
        Directory.CreateDirectory(root);
        IScenarioVariant variant = spec.Variant ?? new StandardScenario(spec.Mode == "fixture-bad");
        string deployment = live ? spec.AgentDeployment ?? UnitEnvironment.Optional("EVAL_CANDIDATE_DEPLOYMENT_NAME") ??
            UnitEnvironment.Required("AZURE_OPENAI_DEPLOYMENT_NAME") : "none";
        string judgeDeployment = live ? UnitEnvironment.Required("EVAL_JUDGE_DEPLOYMENT_NAME") : "none";
        AzureOpenAIClient? azure = live ? new AzureOpenAIClient(UnitEnvironment.Endpoint(), new AzureCliCredential()) : null;
        using IChatClient? judgeClient = azure?.GetChatClient(judgeDeployment).AsIChatClient();
        ReportingConfiguration? reporting = judgeClient is null ? null : DiskBasedReportingConfiguration.Create(
            storageRootPath: Path.Combine(root, "judged"),
            evaluators: [new RelevanceEvaluator(), new GroundednessEvaluator()],
            chatConfiguration: new ChatConfiguration(judgeClient), enableResponseCaching: false, executionName: runId);
        var outcomes = new List<Outcome>();

        foreach (TestCase test in Cases.All)
        for (int iteration = 1; iteration <= repetitions; iteration++)
        {
            var tools = new SupportTools();
            var checks = new Dictionary<string, bool>();
            var scores = new Dictionary<string, Score>();
            string raw = "";
            string? error = null;
            long agentMs = 0;
            var watch = Stopwatch.StartNew();
            using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                if (azure is null) raw = variant.CreateFixture(test, tools);
                else
                {
                    // Each sample has new tools, a new agent and a new implicit conversation.
                    AIAgent agent = azure.GetChatClient(deployment).AsAIAgent(
                        new ChatClientAgentOptions
                        {
                            Name = "evaluated-service-desk",
                            ChatOptions = new ChatOptions
                            {
                                Instructions = variant.Instructions, MaxOutputTokens = 500,
                                ResponseFormat = ChatResponseFormat.Json,
                                Tools = [AIFunctionFactory.Create(tools.GetServiceStatus)]
                            }
                        },
                        clientFactory: inner => new FunctionInvokingChatClient(inner) { MaximumIterationsPerRequest = 3 });
                    raw = (await agent.RunAsync(test.Query, cancellationToken: budget.Token)).Text;
                }
                watch.Stop();
                agentMs = watch.ElapsedMilliseconds; // Judge time is deliberately excluded.
                checks = Contract.Check(test, raw, tools.Calls);
                variant.Check(test, raw, tools.Calls, checks);
                if (reporting is not null)
                {
                    await using ScenarioRun scenario = await reporting.CreateScenarioRunAsync(
                        $"ServiceDesk.{test.Id}", iterationName: iteration.ToString(), cancellationToken: budget.Token);
                    ChatMessage[] messages = [new(ChatRole.System, variant.Instructions), new(ChatRole.User, test.Query)];
                    var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, raw));
                    string grounding = JsonSerializer.Serialize(tools.Calls, Cases.Json) +
                        "\nThese are synthetic observations. Recovery time is unknown. No repair or notification has been executed.";
                    EvaluationResult evaluation = await scenario.EvaluateAsync(messages, response,
                        additionalContext: [new GroundednessEvaluatorContext(grounding)], cancellationToken: budget.Token);
                    foreach (string name in Contract.RequiredMetrics)
                        scores[name] = MetricValidation.Read(evaluation.Metrics.TryGetValue(name, out var metric) ? metric as NumericMetric : null);
                }
            }
            catch (Exception ex) { error = ex.GetType().Name; }
            finally
            {
                if (watch.IsRunning) { watch.Stop(); agentMs = watch.ElapsedMilliseconds; }
            }
            var outcome = new Outcome(test.Id, iteration, raw, tools.Calls.ToArray(), checks, scores, agentMs, error, live);
            outcomes.Add(outcome);
            Console.WriteLine($"{test.Id}/{iteration}: {(outcome.Passed ? "PASS" : "FAIL")}; agentMs={agentMs}; error={error ?? "none"}");
        }
        int exit = ExitCode(outcomes, live);
        var report = new RunReport(runId, spec.Mode, variant.Id, Cases.Version, repetitions,
            Cases.Hash(variant.Instructions), Cases.DatasetHash, Cases.FixtureHash, deployment,
            live ? spec.AgentIdentity is not null ? spec.AgentIdentity.Name : UnitEnvironment.Optional("EVAL_AGENT_MODEL_NAME") : null,
            live ? spec.AgentIdentity is not null ? spec.AgentIdentity.Version : UnitEnvironment.Optional("EVAL_AGENT_MODEL_VERSION") : null, judgeDeployment,
            live ? UnitEnvironment.Optional("EVAL_JUDGE_MODEL_NAME") : null,
            live ? UnitEnvironment.Optional("EVAL_JUDGE_MODEL_VERSION") : null,
            live ? "run" : "not_run", "10.10.0", "1.24.0", 4, UnitEnvironment.Optional("EVAL_COMMIT"),
            DateTimeOffset.UtcNow, exit, outcomes.ToArray());
        string file = await Reports.SaveAsync(report, root);
        Console.WriteLine($"Report: {file}; exitCode={exit}; judgeStatus={report.JudgeStatus}");
        return new(report, file, root);
    }

    public static int ExitCode(IEnumerable<Outcome> outcomes, bool judgeRequired)
    {
        Outcome[] all = outcomes.ToArray();
        if (all.Length == 0 || all.Any(x => x.Error is not null || judgeRequired &&
            Contract.RequiredMetrics.Any(k => !x.Scores.TryGetValue(k, out Score? s) || !s.Valid))) return 2;
        return all.All(x => x.Passed) ? 0 : 1;
    }
}
