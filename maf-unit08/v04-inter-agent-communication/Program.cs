using System.Text.Json;
using Unit08.Common;

return await ExampleHost.RunAsync(args, "v04", "combined", async context =>
{
    Delegation[] plan = Supervisor.Plan(context.NewCase());
    var exchanges = new List<object>();
    foreach (Delegation assigned in plan)
    {
        string requestJson = JsonSerializer.Serialize(assigned);
        Delegation delivered = JsonSerializer.Deserialize<Delegation>(requestJson)
            ?? throw new InvalidOperationException("empty-delegation");
        Report result = await context.Supervisor.ConsultAsync(delivered,
            context.Arguments.RunOptions, context.CancellationToken);
        if (result.Outcome != "accepted")
            return context.Result("needs-review", new { Report = result }, 2);
        Report returned = JsonSerializer.Deserialize<Report>(JsonSerializer.Serialize(result))!;
        Contract.ValidateReport(assigned, returned);

        string rejected;
        try
        {
            Contract.ValidateReport(assigned, returned with
                { Task = returned.Task with { RunId = "another-run" } });
            throw new Exception("A cross-run report was accepted.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "assignment-mismatch")
            { rejected = ex.Message; }
        exchanges.Add(new { Request = delivered, Response = returned, RejectedForgedBinding = rejected });
    }
    return context.Result("communication-verified", new
        { Transport = "in-process JSON round trip", Exchanges = exchanges });
});
