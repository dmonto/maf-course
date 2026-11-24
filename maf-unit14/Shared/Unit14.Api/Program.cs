using Unit14;
try
{
    EnvironmentFile.LoadWhenAvailable();
    string mode = Environment.GetEnvironmentVariable("Runner__Mode") ?? "fixture";
    string? address = Environment.GetEnvironmentVariable("MAF_UNIT14_CONTAINER") == "true"
        ? null : "http://127.0.0.1:8080";
    await using var app = AgentHost.Build(ApiSettings.Read(mode), address);
    await app.RunAsync();
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"Host failed: {error.GetType().Name}: {error.Message}");
    return 2;
}
