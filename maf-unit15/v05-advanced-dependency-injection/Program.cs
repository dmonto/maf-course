using Maf.Unit15.Application;
using Maf.Unit15.Domain;
using Maf.Unit15.Examples;
using Maf.Unit15.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

return await ExampleRunner.RunAsync("v05", args, async ctx =>
{
    LabConsole.Write("MILESTONE", "DI scopes: two scopes get different CountingEvidence instances; a singleton capturing a scoped service must fail validation.");
    var stores = ctx.Stores();
    var services = new ServiceCollection();
    services.AddSingleton<IEvidenceReader>(ctx.Reader);
    services.AddSingleton<IAssessmentStore>(stores.Producer);
    services.AddScoped<CountingEvidence>();
    services.AddScoped(sp => new AssessmentService(sp.GetRequiredService<IAssessmentStore>(), sp.GetRequiredService<CountingEvidence>()));
    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    using var scopeA = provider.CreateScope();
    using var scopeB = provider.CreateScope();
    var appA = scopeA.ServiceProvider.GetRequiredService<AssessmentService>();
    var appB = scopeB.ServiceProvider.GetRequiredService<AssessmentService>();
    var first = await appA.AssessAsync(ctx.OperationId + "-a", ctx.Service, ctx.Token);
    var retried = await appA.AssessAsync(ctx.OperationId + "-a", ctx.Service, ctx.Token);
    await appB.AssessAsync(ctx.OperationId + "-b", ctx.Service, ctx.Token);
    var readerA = scopeA.ServiceProvider.GetRequiredService<CountingEvidence>();
    var readerB = scopeB.ServiceProvider.GetRequiredService<CountingEvidence>();
    bool invalidCaptureDetected = false;
    try
    {
        using var invalid = new ServiceCollection().AddScoped<ScopeStamp>().AddSingleton<InvalidOwner>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
    catch (AggregateException) { invalidCaptureDetected = true; }
    catch (InvalidOperationException) { invalidCaptureDetected = true; }
    RunContext.Check("Each scope used its own adapter instance exactly once", readerA.Calls == 1 && readerB.Calls == 1 && readerA.Id != readerB.Id);
    RunContext.Check("ValidateScopes/ValidateOnBuild rejected a singleton capturing a scoped service", invalidCaptureDetected);
    return new { scopeA = new { id = readerA.Id, adapterCalls = readerA.Calls }, scopeB = new { id = readerB.Id, adapterCalls = readerB.Calls },
        retryReusedAcceptedResult = first == retried, invalidCaptureDetected, state = await stores.InspectAsync(ctx.Token) };
});

sealed class CountingEvidence(IEvidenceReader inner) : IEvidenceReader
{
    public Guid Id { get; } = Guid.NewGuid();
    public int Calls { get; private set; }
    public Task<Evidence> ReadAsync(string service, CancellationToken ct) { Calls++; return inner.ReadAsync(service, ct); }
}
sealed class ScopeStamp { }
sealed class InvalidOwner(ScopeStamp stamp) { public ScopeStamp Stamp { get; } = stamp; }
