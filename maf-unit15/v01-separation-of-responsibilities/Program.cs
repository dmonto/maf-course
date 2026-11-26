using Maf.Unit15.Application;
using Maf.Unit15.Domain;
using Maf.Unit15.Examples;
using Maf.Unit15.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

return await ExampleRunner.RunAsync("v01", args, async ctx =>
{
    LabConsole.Write("MILESTONE", "Three owners: IEvidenceReader (evidence), AssessmentService (decision + commit), ReviewConsumer (review queue).");
    var stores = ctx.Stores();
    var assessment = await stores.Assessment(ctx.Reader).AssessAsync(ctx.OperationId, ctx.Service, ctx.Token);
    var state = await stores.InspectAsync(ctx.Token);
    LabConsole.Write("CONCLUSION", $"Assessment committed with decision {assessment.Decision}; the consumer has not run, so no review item exists yet.");
    ctx.Emit("assessment-committed", new { assessment.Decision });
    return new { ownership = new { evidence = "IEvidenceReader", assessment = "AssessmentService", review = "ReviewConsumer" },
        assessment, state, businessActionsExecuted = Array.Empty<string>() };
});


