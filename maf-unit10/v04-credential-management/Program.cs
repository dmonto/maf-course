return await Lab.RunAsync(args, "v04", context =>
{
    string[] names = ["AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_DEPLOYMENT_NAME", "CORPORATE_API_BASE_URL",
        "CORPORATE_API_TOKEN", "SLACK_CHANNEL_ID", "SLACK_BOT_TOKEN"];
    var settings = names.Select(name => new { setting = name,
        configured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)) }).ToArray();
    var authentication = new { azure = "AzureCliCredential",
        corporate = context.Demo ? "synthetic fixture bearer" : "configured corporate bearer",
        slack = context.Demo ? "synthetic fixture bearer" : "configured Slack or fixture bearer" };

    // Readable summary; presence only, never a value, length or fingerprint.
    // The RESULT JSON line printed afterwards stays last.
    Console.WriteLine($"Mode: {(context.Demo ? "local-demo" : "configured-live")}   Secret values printed: no   Dependency requests: 0");
    Console.WriteLine("\nSettings (presence only):");
    foreach (var s in settings)
        Console.WriteLine($"  {(s.configured ? "set    " : "missing")}  {s.setting}");
    Console.WriteLine("\nAuthentication:");
    Console.WriteLine($"  Azure OpenAI:  {authentication.azure}");
    Console.WriteLine($"  Corporate API: {authentication.corporate}");
    Console.WriteLine($"  Slack:         {authentication.slack}\n");

    return Task.FromResult<object>(new { settings, authentication,
        secretValuesPrinted = false, dependencyRequests = 0 });
});
