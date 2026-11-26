using Maf.Unit15.Application;
using Maf.Unit15.Domain;
using Maf.Unit15.Examples;
using Maf.Unit15.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

return await ExampleRunner.RunAsync("v09", args, async ctx =>
{
    var stores = ctx.Stores();
    var assessment = await stores.Assessment(ctx.Reader).AssessAsync(ctx.OperationId, ctx.Service, ctx.Token);
    await stores.Dispatcher.DispatchAsync(false, ctx.Token);
    var handoff = await stores.InspectAsync(ctx.Token);
    if (assessment.Decision == "no_action") return new { assessment, handoff, process = "not-required", externalRepairExecuted = false };
    LabConsole.Write("MILESTONE", "Modular repair process: a state machine with owned transitions. No external repair is executed.");
    var workflow = new RepairProcess(Path.Combine(ctx.State, "repair-process.db"));
    string successful = ctx.OperationId + "-success";
    workflow.Open(successful, ctx.Service);
    string premature;
    try { workflow.Apply(successful, "premature-request", "request-repair", "operator-demo"); throw new InvalidOperationException("Approval bypassed."); }
    catch (TransitionRejected) { premature = "blocked-before-approval"; }
    workflow.Apply(successful, "approval-1", "approve", "reviewer-demo");
    workflow.Apply(successful, "request-1", "request-repair", "operator-demo");
    var retry = workflow.Apply(successful, "request-1", "request-repair", "operator-demo");
    workflow.Apply(successful, "verify-1", "verify-success", "verifier-demo");
    string failed = ctx.OperationId + "-manual-recovery";
    workflow.Open(failed, ctx.Service);
    workflow.Apply(failed, "approval-2", "approve", "reviewer-demo");
    workflow.Apply(failed, "request-2", "request-repair", "operator-demo");
    workflow.Apply(failed, "verify-2", "verify-failure", "verifier-demo");
    return new { assessment, handoff, prematureRequest = premature, duplicateCommand = retry,
        successful = workflow.Inspect(successful), manualRecovery = workflow.Inspect(failed), externalRepairExecuted = false };
});


