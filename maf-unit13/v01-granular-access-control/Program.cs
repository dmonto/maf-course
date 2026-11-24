using Unit13;
using System.Text.Json;
using System.Security.Claims;

return await Entry.RunAsync("v01", args, context =>
{
    context.FixtureOnly();
    List<object> results = [];
    foreach (Scenario test in Fixtures.Cases())
    {
        MemoryAuditSink audit = new();
        SecurityTools tools = new(() => test.Caller, test.Service, Guid.NewGuid().ToString("N"), audit);
        Evidence evidence = tools.GetServiceStatus(test.Service);
        Lab.Require(evidence.Status == test.Expected, "Authorization outcome differs from the contract.");
        results.Add(new { test.Id, observed = evidence.Status, audit.Events[0].Decision, audit.Events[0].Reason });
    }
    context.Save("authorization.json", results);
    Lab.Show(new { cases = results.Count, passed = true });
    return Task.FromResult(0);
});
