using System.Diagnostics;
using Unit08.Common;

return await ExampleHost.RunAsync(args, "v08", "combined", async context =>
{
    int count = DotEnv.Number(context.Arguments.Get("--cases"), 2, 1, 10);
    SupportCase[] cases = Enumerable.Range(0, count).Select(_ => context.NewCase()).ToArray();
    Supervisor supervisor = context.Supervisor;
    var watch = Stopwatch.StartNew();
    CaseOutcome[] outcomes = await Task.WhenAll(cases.Select(input =>
        supervisor.RunAsync(input, context.Arguments.RunOptions, context.CancellationToken)));
    watch.Stop();
    bool review = outcomes.Any(o => o.Stage == "needs-review");
    return context.Result(review ? "needs-review" : "scalability-observed", new
    {
        Cases = count, ElapsedMs = watch.ElapsedMilliseconds,
        PerCaseSpecialistLimit = context.Settings.Concurrency,
        ObservedHostPeak = supervisor.Metrics.PeakSpecialistConcurrency,
        supervisor.Metrics.SpecialistInvocations, supervisor.Metrics.SynthesisInvocations,
        LimiterScope = "per case; no service-wide limiter is installed", Outcomes = outcomes
    }, review ? 2 : 0);
});
