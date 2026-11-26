using System.Diagnostics;
using System.Text.Json;
using Maf.Unit15.Application;
using Maf.Unit15.Infrastructure;

namespace Maf.Unit15.Examples;

public sealed record LabOptions(string Root, string Mode, string Service, string BaseUrl,
    string StateDirectory, string OperationId)
{
    public static LabOptions Parse(string example, string[] args)
    {
        string root = EnvironmentFile.Load();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length || args[i] is not ("--mode" or "--service" or "--state" or "--operation-id" or "--base"))
                throw new ArgumentException("Use --mode, --service, --state, --operation-id or --base with a value.");
            values[args[i]] = args[i + 1];
        }
        string Get(string key, string env, string fallback) => values.GetValueOrDefault(key) ?? Environment.GetEnvironmentVariable(env) ?? fallback;
        string mode = Get("--mode", "UNIT15_MODE", "fixture");
        string service = Get("--service", "UNIT15_SERVICE", "vpn");
        if (mode is not ("fixture" or "http" or "azure") || service is not ("vpn" or "email")) throw new ArgumentException("Invalid mode or service.");
        string runId = Guid.NewGuid().ToString("N");
        string artifacts = Environment.GetEnvironmentVariable("UNIT15_ARTIFACTS_DIR") ?? "artifacts";
        string state = values.GetValueOrDefault("--state") ?? Path.Combine(root, artifacts, example, runId);
        return new(root, mode, service, Get("--base", "UNIT15_EVIDENCE_BASE_URL", "http://127.0.0.1:8082"),
            Path.GetFullPath(state), values.GetValueOrDefault("--operation-id") ?? $"{example}-{runId[..12]}");
    }
}

public sealed class RunContext : IDisposable
{
    public LabOptions Options { get; }
    public CancellationToken Token { get; }
    public string State => Options.StateDirectory;
    public string OperationId => Options.OperationId;
    public string Service => Options.Service;
    private IEvidenceReader? reader;
    public IEvidenceReader Reader => reader ??= Options.Mode switch
    {
        "fixture" => new FixtureEvidenceReader(),
        "http" => new HttpEvidenceReader(Options.BaseUrl),
        "azure" => new MafEvidenceReader(),
        _ => throw new ArgumentException("Unknown evidence mode.")
    };
    public RunContext(LabOptions options, CancellationToken token)
    {
        Options = options; Token = token;
        Directory.CreateDirectory(State);
    }
    public LabStores Stores(string? suffix = null) => new(suffix is null ? State : Path.Combine(State, suffix));
    public void Emit(string stage, object? data = null)
    {
        string line = EventContract.Serialize(new { atUtc = DateTimeOffset.UtcNow,
            correlationId = OperationId, stage, traceId = Activity.Current?.TraceId.ToString(), data });
        File.AppendAllText(Path.Combine(State, "events.jsonl"), line + Environment.NewLine);
        Console.Error.WriteLine(line);
        LabConsole.Write("EVENT", $"{stage} (structured JSON line also appended to events.jsonl)");
    }
    public void Save(string name, object result)
    {
        File.WriteAllText(Path.Combine(State, name),
            JsonSerializer.Serialize(result, new JsonSerializerOptions(EventContract.Json) { WriteIndented = true }));
        LabConsole.Write("ARTIFACT", Path.Combine(State, name));
    }
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    // Positive, named check: prints PASS/FAIL and throws on FAIL.
    public static void Check(string name, bool condition) => LabConsole.Check(name, condition);
    public void Dispose() { if (reader is IDisposable disposable) disposable.Dispose(); }
}

public sealed class LabStores
{
    public AssessmentStore Producer { get; }
    public ReviewConsumer Review { get; }
    public AssessmentService Assessment(IEvidenceReader reader) => new(Producer, reader);
    public OutboxDispatcher Dispatcher => new(Producer, Review);
    public LabStores(string directory)
    {
        Directory.CreateDirectory(directory);
        Producer = new(Path.Combine(directory, "assessments.db"));
        Review = new(Path.Combine(directory, "reviews.db"));
    }
    public async Task<StoreView> InspectAsync(CancellationToken ct) => new(
        await Producer.AssessmentsAsync(ct), (await Producer.PendingAsync(ct)).Count,
        await Review.Inbox.CountAsync("inbox", ct), await Review.ItemsAsync(ct));
}

public static class ExampleRunner
{
    public static async Task<int> RunAsync(string example, string[] args, Func<RunContext, Task<object>> demonstration)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; budget.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            using var ctx = new RunContext(LabOptions.Parse(example, args), budget.Token);
            LabConsole.Write("START", $"Unit 15 {example} | mode={ctx.Options.Mode} | service={ctx.Service} | operationId={ctx.OperationId}");
            LabConsole.Write("MODE", ctx.Options.Mode switch
            {
                "fixture" => "Evidence comes from a built-in fixture. No HTTP, no Azure, no model.",
                "http" => $"Evidence is read over HTTP from the local evidence host at {ctx.Options.BaseUrl}.",
                _ => "Evidence is produced by a real MAF agent calling Azure OpenAI (identity per Runner__Identity)."
            });
            LabConsole.Write("MILESTONE", $"State directory (SQLite files, events.jsonl, result.json): {ctx.State}");
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name.StartsWith("Maf.Unit15", StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => ctx.Emit("span-completed", new
                { name = activity.DisplayName, traceId = activity.TraceId.ToString(), spanId = activity.SpanId.ToString(),
                    parentSpanId = activity.ParentSpanId.ToString(), elapsedMs = activity.Duration.TotalMilliseconds, status = activity.Status.ToString() })
            };
            ActivitySource.AddActivityListener(listener);
            using var run = Unit15Diagnostics.Source.StartActivity(example);
            run?.SetTag("operation.id", ctx.OperationId);
            object result = await demonstration(ctx);
            var report = new { status = "ok", example, mode = ctx.Options.Mode,
                stateDirectory = ctx.State, result };
            ctx.Save("result.json", report);
            LabConsole.Write("CONCLUSION", $"{example} completed. Review the milestones, checks and artifacts printed above.");
            Console.WriteLine("RESULT " + EventContract.Serialize(report));
            return 0;
        }
        catch (Exception error)
        {
            LabConsole.Write("FAILURE", $"{error.GetType().Name}: {error.Message}");
            LabConsole.Write("CONCLUSION", "The example did not qualify. No success is inferred from a failure.");
            Console.Error.WriteLine("RESULT " + EventContract.Serialize(new
                { status = "error", example, errorType = error.GetType().Name, message = error.Message }));
            return 1;
        }
        finally { Console.CancelKeyPress -= cancel; }
    }
}
