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
    Output.Result(new { executionMode = c.ExecutionMode, modelVisibleArguments = properties,
        toolSchema = schema, trustedFilter = CallerScope.ForProfile("employee").Filter(c.Settings.CatalogVersion), evidence = result });
});
internal sealed class BoundKnowledgeTool(LabContext context, CallerScope scope)
{
    public Task<SearchHit[]> SearchAsync([Description("A support knowledge question.")] string query) =>
        context.Store().SearchAsync(scope, "hybrid", query, context.Token);
}
