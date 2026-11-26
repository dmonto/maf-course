using Maf.Unit15.Application;
using Maf.Unit15.Domain;
using Maf.Unit15.Examples;
using Maf.Unit15.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

return await ExampleRunner.RunAsync("v10", args, async ctx =>
{
    var stores = ctx.Stores();
    await stores.Assessment(ctx.Reader).AssessAsync(ctx.OperationId, ctx.Service, ctx.Token);
    string payload = await stores.Producer.PayloadAsync(ctx.OperationId, ctx.Token);
    var reviewDelivery = await stores.Dispatcher.DispatchAsync(false, ctx.Token);
    var reviewState = await stores.InspectAsync(ctx.Token);
    LabConsole.Write("MILESTONE", "Scalability: add an independent AuditConsumer without touching the producer; simulate its outage and replay.");
    var audit = new AuditConsumer(Path.Combine(ctx.State, "audit.db"));
    var before = new { inboxCount = await audit.Inbox.CountAsync("inbox", ctx.Token), auditRecords = await audit.Inbox.CountAsync("audit_records", ctx.Token) };
    string failure;
    try { await DeliverAuditAsync(audit, payload, false, ctx.Token); throw new InvalidOperationException("The audit outage was not observed."); }
    catch (IOException error) { failure = error.Message; }
    var replays = new List<string>();
    for (int i = 0; i < 3; i++) replays.Add(await DeliverAuditAsync(audit, payload, true, ctx.Token));
    var after = new { inboxCount = await audit.Inbox.CountAsync("inbox", ctx.Token), auditRecords = await audit.Inbox.CountAsync("audit_records", ctx.Token) };
    RunContext.Check("Audit replay: applied once then duplicates, exactly one audit record", after.auditRecords == 1 && replays.SequenceEqual(new[] { "applied", "duplicate", "duplicate" }));
    return new { reviewDelivery, reviewState, duringAuditOutage = new { auditState = before, failure }, auditReplay = replays, auditState = after };
});

static Task<string> DeliverAuditAsync(AuditConsumer audit, string payload, bool available, CancellationToken ct)
{
    if (!available) { LabConsole.Write("FAULT", "Audit subscription is down; the event stays in the outbox/is retried."); throw new IOException("Synthetic audit subscription unavailable"); }
    return audit.ConsumeAsync(payload, ct);
}
