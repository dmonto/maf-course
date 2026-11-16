using System.Text.Json;

namespace Unit08.Common;

public sealed record ExampleResult(string Example, string Mode, string Stage, object Data);
public sealed record ExampleExecution(ExampleResult Result, int ExitCode = 0);

public sealed class ExampleContext(string id, Arguments arguments, RuntimeSettings settings, CancellationToken ct)
{
    public Arguments Arguments { get; } = arguments;
    public RuntimeSettings Settings { get; } = settings;
    public CancellationToken CancellationToken { get; } = ct;
    public AuditTrail Audit { get; } = new();
    private Supervisor? _supervisor;
    public Supervisor Supervisor => _supervisor ??= new Supervisor(
        Settings.Live ? new LiveBackend(Settings) : new DemoBackend(Settings), Settings, Audit);
    public SupportCase NewCase(string? scenario = null) =>
        new(Guid.NewGuid().ToString("N"), scenario ?? Arguments.Scenario);
    public ExampleExecution Result(string stage, object data, int exitCode = 0) =>
        new(new(id, Settings.Live ? "live" : "demo", stage, data), exitCode);
    public ExampleExecution Complete(CaseOutcome outcome) => Result(outcome.Stage, outcome,
        outcome.Stage == "needs-review" ? 2 : 0);
}

public static class ExampleHost
{
    public static async Task<int> RunAsync(string[] args, string id, string defaultScenario,
        Func<ExampleContext, Task<ExampleExecution>> body, bool supportsLive = true,
        params string[] extraFlags)
    {
        try
        {
            var arguments = new Arguments(args, defaultScenario, extraFlags);
            if (arguments.Get("--cases") is not null && id != "v08")
                throw new ArgumentException("--cases belongs to v08.");
            var settings = RuntimeSettings.Load(arguments);
            if (settings.Live && !supportsLive)
                throw new ArgumentException("This policy-only example supports --demo.");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(settings.RunTimeoutSeconds));
            var result = await body(new(id, arguments, settings, deadline.Token));
            Console.WriteLine(JsonSerializer.Serialize(result.Result,
                new JsonSerializerOptions { WriteIndented = true }));
            return result.ExitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                Event = "run-failed", Example = id, ErrorType = ex.GetType().Name,
                Code = ex is OperationCanceledException ? "run-cancelled" : "invalid-input-configuration-or-result",
                // Opt-in diagnostics only: messages from live calls may contain endpoint details.
                Detail = Environment.GetEnvironmentVariable("MAF_DEBUG") == "1" ? ex.ToString() : null
            }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
            return 1;
        }
    }
}
