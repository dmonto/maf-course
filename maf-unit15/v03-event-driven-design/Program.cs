using Maf.Unit15.Application;
using Maf.Unit15.Domain;
using Maf.Unit15.Examples;
using Maf.Unit15.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

return await ExampleRunner.RunAsync("v03", args, async ctx =>
{
    LabConsole.Write("MILESTONE", "Event-driven: commit assessment + outbox event, deliver to the consumer, then replay the same event.");
    var stores = ctx.Stores();
    var assessment = await stores.Assessment(ctx.Reader).AssessAsync(ctx.OperationId, ctx.Service, ctx.Token);
    string payload = await stores.Producer.PayloadAsync(ctx.OperationId, ctx.Token);
    var afterCommit = await stores.InspectAsync(ctx.Token);
    var delivery = await stores.Dispatcher.DispatchAsync(false, ctx.Token);
    string replay = await stores.Review.ConsumeAsync(payload, ctx.Token);
    RunContext.Check("Replaying the same event is reported as duplicate (no second effect)", replay == "duplicate");
    ctx.Save("AssessmentRecorded.v1.json", EventContract.Parse(payload));
    return new { assessment, afterCommit, delivery, replay, final = await stores.InspectAsync(ctx.Token) };
});


