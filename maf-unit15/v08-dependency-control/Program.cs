using Maf.Unit15.Application;
using Maf.Unit15.Domain;
using Maf.Unit15.Examples;
using Maf.Unit15.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

return await ExampleRunner.RunAsync("v08", args, async ctx =>
{
    string[] domainReferences = typeof(AssessmentPolicy).Assembly.GetReferencedAssemblies().Select(x => x.Name!).Order().ToArray();
    string[] applicationReferences = typeof(AssessmentService).Assembly.GetReferencedAssemblies().Select(x => x.Name!).Order().ToArray();
    string[] forbidden = domainReferences.Concat(applicationReferences)
        .Where(x => x.StartsWith("Azure", StringComparison.Ordinal) || x.StartsWith("Microsoft.Agents", StringComparison.Ordinal) || x.StartsWith("Microsoft.Data.Sqlite", StringComparison.Ordinal)).ToArray();
    LabConsole.Write("MILESTONE", $"Domain references: {string.Join("; ", domainReferences)}");
    LabConsole.Write("MILESTONE", $"Application references: {string.Join("; ", applicationReferences)}");
    RunContext.Check("No Azure, MAF or SQLite assembly is referenced by the Domain/Application core", forbidden.Length == 0);
    var stores = ctx.Stores();
    var assessment = await stores.Assessment(new FixtureEvidenceReader()).AssessAsync(ctx.OperationId, "email", ctx.Token);
    await stores.Dispatcher.DispatchAsync(false, ctx.Token);
    return new { domainReferences, applicationReferences, forbiddenProviderDependencies = forbidden, modelCalled = false,
        assessment, state = await stores.InspectAsync(ctx.Token) };
});


