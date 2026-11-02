using Unit02.Common;

return await CourseApp.RunAsync(args, async context =>
{
    string a = context.Model("FOUNDRY_MODEL_A", "demo-classifier-a");
    string b = context.Model("FOUNDRY_MODEL_B", "demo-classifier-b");
    if (a == b) throw new ConfigurationException("Assign two distinct Foundry model deployments.");
    var evidence = new List<object>();
    bool connectionFailed = false;
    foreach (var deployment in new[] { a, b })
    {
        var connection = context.Connect("foundry", deployment);
        var runs = new List<RunRecord>();
        foreach (var ticket in Tasks.Tickets)
        {
            var result = await Inference.RunAsync(context, connection, Tasks.ClassifierInstructions,
                ticket.Text, ticket.Label, text => Tasks.CheckLabel(text, ticket.Label));
            Inference.Print(result);
            runs.Add(result.Record);
        }
        connectionFailed |= runs.Any(r => r.Status != "ok");
        evidence.Add(new { deployment, correct = runs.Count(r => r.QualityPassed), total = runs.Count, runs });
    }
    context.Save("v03-classifiers.json", evidence);
    Console.WriteLine("Use assigned deployments from the Foundry catalog and the same three ticket labels.");
    if (connectionFailed) throw new InvalidOperationException();
});
