using Maf.Unit15.Application;
using Maf.Unit15.Domain;
using Maf.Unit15.Examples;
using Maf.Unit15.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

return await ExampleRunner.RunAsync("v02", args, async ctx =>
{
    LabConsole.Write("MILESTONE", $"Integration boundary: the application only knows IEvidenceReader; the adapter is {ctx.Reader.GetType().Name}.");
    var reader = new RequestEvidence(ctx.Reader);
    var stores = ctx.Stores();
    var assessment = await stores.Assessment(reader).AssessAsync(ctx.OperationId, ctx.Service, ctx.Token);
    LabConsole.Write("CONCLUSION", "Swapping fixture/http/azure changes the adapter, never the application code.");
    return new { adapter = ctx.Reader.GetType().Name, transport = ctx.Options.Mode == "http" ? "HTTP" : ctx.Options.Mode,
        evidenceResponse = reader.Last, assessment, state = await stores.InspectAsync(ctx.Token) };
});

sealed class RequestEvidence(IEvidenceReader inner) : IEvidenceReader
{
    public Evidence? Last { get; private set; }
    public async Task<Evidence> ReadAsync(string service, CancellationToken ct) => Last = await inner.ReadAsync(service, ct);
}
