using Maf.Unit15.Application;
using Maf.Unit15.Domain;
using Maf.Unit15.Examples;
using Maf.Unit15.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

return await ExampleRunner.RunAsync("v07", args, async ctx =>
{
    LabConsole.Write("MILESTONE", "Observability: one operationId/correlationId and one trace follow the evidence read, the commit and the delivery (see events.jsonl).");
    var stores = ctx.Stores();
    var observer = new ObservedEvidence(ctx.Reader, ctx);
    var assessment = await stores.Assessment(observer).AssessAsync(ctx.OperationId, ctx.Service, ctx.Token);
    var e = EventContract.Parse(await stores.Producer.PayloadAsync(ctx.OperationId, ctx.Token));
    ctx.Emit("outbox-pending", new { e.EventId });
    var deliveries = await stores.Dispatcher.DispatchAsync(false, ctx.Token);
    var state = await stores.InspectAsync(ctx.Token);
    ctx.Emit("business-state-inspected", new { state.InboxCount, state.PendingEvents, reviewItems = state.ReviewItems.Count });
    return new { assessment, operationId = ctx.OperationId, e.CorrelationId, e.EventId,
        traceId = Activity.Current?.TraceId.ToString(), evidenceResponse = observer.Last,
        deliveries, state, diagnosticsFile = Path.Combine(ctx.State, "events.jsonl") };
});

sealed class ObservedEvidence(IEvidenceReader inner, RunContext ctx) : IEvidenceReader
{
    public Evidence? Last { get; private set; }
    public async Task<Evidence> ReadAsync(string service, CancellationToken ct)
    {
        using var span = Unit15Diagnostics.Source.StartActivity("evidence-read");
        Last = await inner.ReadAsync(service, ct);
        ctx.Emit("evidence-observed", new { Last.Service, evidenceMode = Last.Mode, Last.RequestId, Last.TraceId });
        return Last;
    }
}
