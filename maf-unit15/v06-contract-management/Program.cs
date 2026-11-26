using Maf.Unit15.Application;
using Maf.Unit15.Domain;
using Maf.Unit15.Examples;
using Maf.Unit15.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

return await ExampleRunner.RunAsync("v06", args, async ctx =>
{
    LabConsole.Write("MILESTONE", "Contract management: mutate the accepted event and show each invalid variant is rejected before any write.");
    var stores = ctx.Stores();
    await stores.Assessment(ctx.Reader).AssessAsync(ctx.OperationId, ctx.Service, ctx.Token);
    string payload = await stores.Producer.PayloadAsync(ctx.OperationId, ctx.Token);
    var accepted = EventContract.Parse(payload);
    await stores.Dispatcher.DispatchAsync(false, ctx.Token);
    var checks = new List<object>();
    foreach (var (label, mutate) in new (string, Action<JsonObject>)[]
    {
        ("unsupported-schema", n => n["schemaVersion"] = 2),
        ("correlation-mismatch", n => n["correlationId"] = "another-operation"),
        ("unsupported-mode", n => n["data"]!["evidence_mode"] = "unknown")
    })
    {
        var changed = JsonNode.Parse(payload)!.AsObject();
        mutate(changed);
        try { EventContract.Parse(changed.ToJsonString()); throw new InvalidOperationException("An invalid contract was accepted."); }
        catch (InvalidDataException) { checks.Add(new { @case = label, outcome = "rejected-before-write" }); LabConsole.Write("CHECK", ($"PASS | {label} rejected")); }
    }
    var conflicting = accepted with { Data = accepted.Data with { EvidenceMode = accepted.Data.EvidenceMode == "fixture" ? "azure" : "fixture" } };
    try { await stores.Review.ConsumeAsync(EventContract.Serialize(conflicting), ctx.Token); throw new InvalidOperationException("Conflicting identity accepted."); }
    catch (InvalidDataException) { checks.Add(new { @case = "same-id-different-payload", outcome = "rejected" }); LabConsole.Write("CHECK", "PASS | same event ID with different payload rejected"); }
    ctx.Save("accepted-contract.json", accepted);
    return new { accepted, checks, state = await stores.InspectAsync(ctx.Token) };
});


