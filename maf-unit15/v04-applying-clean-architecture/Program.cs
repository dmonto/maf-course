using Maf.Unit15.Application;
using Maf.Unit15.Domain;
using Maf.Unit15.Examples;
using Maf.Unit15.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

return await ExampleRunner.RunAsync("v04", args, async ctx =>
{
    LabConsole.Write("MILESTONE", "Crash recovery: the consumer commits, the dispatcher crashes before marking sent, a restarted dispatcher redelivers.");
    var stores = ctx.Stores();
    var assessment = await stores.Assessment(ctx.Reader).AssessAsync(ctx.OperationId, ctx.Service, ctx.Token);
    var afterAssessment = await stores.InspectAsync(ctx.Token);
    try
    {
        await stores.Dispatcher.DispatchAsync(true, ctx.Token);
        throw new InvalidOperationException("The injected failure did not occur.");
    }
    catch (IOException error) when (error.Message == "Injected crash after consumer commit")
    {
        ctx.Emit("expected-delivery-failure", new { failureType = error.GetType().Name });
    }
    var afterFailure = await stores.InspectAsync(ctx.Token);
    LabConsole.Write("MILESTONE", "Restart: new store objects, same SQLite files. The outbox still has the unsent event.");
    // Each operation owns its connection; create new store objects before recovery.
    var restarted = ctx.Stores();
    var recovery = await restarted.Dispatcher.DispatchAsync(false, ctx.Token);
    var final = await restarted.InspectAsync(ctx.Token);
    RunContext.Check("Redelivery is a duplicate and the outbox is drained", recovery[0].Outcome == "duplicate" && final.PendingEvents == 0);
    RunContext.Check("The business effect happened exactly once", final.ReviewItems.Count == (assessment.Decision == "review_required" ? 1 : 0));
    return new { afterAssessment, afterFailure, recovery, final };
});


