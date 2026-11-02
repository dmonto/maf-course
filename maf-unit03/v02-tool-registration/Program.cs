using System.Text.Json;
using Microsoft.Extensions.AI;
using Unit03.Common;

return await CourseApp.RunAsync(args, async context =>
{
    Console.WriteLine("LOCAL-ONLY: this project invokes a registered function directly; no model calls.");
    var audit = new Audit(Console.Out);
    var repository = new StatusRepository();
    var runtime = new ToolRuntime(ActorContext.Student, context.Limits.MaxToolAttempts, audit);
    var adapter = new ServiceTools(repository, runtime);
    AIFunction function = AIFunctionFactory.Create(adapter.GetServiceStatus);
    Console.WriteLine($"Registered function: {function.Name}");
    Console.WriteLine($"Description: {function.Description}");
    Console.WriteLine(function.JsonSchema);
    // Model-visible input must not contain caller identity, permission lists or a cancellation token.
    string[] samples = [" Payroll ", "crm", "billing", "../../payroll"];
    List<object> reports = [];
    foreach (string service in samples)
    {
        runtime.BeginTurn(Guid.NewGuid().ToString("N"));
        await function.InvokeAsync(new AIFunctionArguments { ["serviceName"] = service });
        var evidence = runtime.Evidence.ToArray();
        Console.WriteLine(JsonSerializer.Serialize(evidence, Audit.JsonOptions));
        reports.Add(new { input = service, evidence });
    }
    context.Save("v02-tool-registration", new { function.Name, schema = function.JsonSchema, reports, repository.Reads });
});
