using System.ComponentModel;
using Microsoft.Extensions.AI;
return await Runner.RunAsync("v06", args, async c =>
{
    var bound = new BoundKnowledgeTool(c, CallerScope.ForProfile("employee"));
    var tool = AIFunctionFactory.Create(bound.SearchAsync, name: "search_knowledge",
        description: "Read authorized support evidence for a question.");
    var schema = tool.JsonSchema;
    var properties = schema.GetProperty("properties").EnumerateObject().Select(x => x.Name).ToArray();
    Output.Require(properties.SequenceEqual(new[] { "query" }), "The tool exposed a caller-controlled security field.");
    var result = await bound.SearchAsync("Payroll batch retry procedure");
    var trustedFilter = CallerScope.ForProfile("employee").Filter(c.Settings.CatalogVersion);

    // Readable summary; the RESULT JSON line below stays last.
    Console.WriteLine($"Execution mode: {c.ExecutionMode}");
    Console.WriteLine($"\nTool: {tool.Name} - {tool.Description}");
    Console.WriteLine($"  Arguments visible to the model: {string.Join(", ", properties)}");
    Console.WriteLine("  PASS  No caller-controlled security field is exposed");
    Console.WriteLine($"\nTrusted filter (applied server-side, not model-controlled):\n  {trustedFilter}");
    Console.WriteLine($"\nEvidence for \"Payroll batch retry procedure\" as 'employee' ({result.Length} hit(s)):");
    if (result.Length == 0) Console.WriteLine("    (no evidence visible)");
    foreach (var h in result)
        Console.WriteLine($"    - {h.Title} ({h.DocumentId}, tenant {h.TenantId})");
    Console.WriteLine();

    Output.Result(new { executionMode = c.ExecutionMode, modelVisibleArguments = properties,
        toolSchema = schema, trustedFilter, evidence = result });
});
internal sealed class BoundKnowledgeTool(LabContext context, CallerScope scope)
{
    public Task<SearchHit[]> SearchAsync([Description("A support knowledge question.")] string query) =>
        context.Store().SearchAsync(scope, "hybrid", query, context.Token);
}
