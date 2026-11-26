using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using Azure.Core;
using Azure.Identity;
using Unit14;

return await Entry.RunAsync("checks", args, async context =>
{
    context.FixtureOnly();
    await ProbeChecks.RunAsync(context);
    await using (var host = await ApiSession.StartAsync(ApiSettings.Read("fixture") with
        { RequestTimeoutSeconds = 1, FixtureDelayMs = 2500 }))
    {
        using var client = new HttpClient { BaseAddress = new Uri(host.Address), Timeout = TimeSpan.FromSeconds(5) };
        using var response = await client.PostAsJsonAsync("/api/status", new { service = "vpn" });
        var failure = await response.Content.ReadFromJsonAsync<JsonElement>();
        Lab.Require(response.StatusCode == HttpStatusCode.GatewayTimeout &&
            failure.GetProperty("phase").GetString() == "fixture" &&
            failure.GetProperty("reason").GetString() == "request_deadline", "Request deadline diagnostics were lost.");
    }
    string? originalEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
    string? originalDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
    try
    {
        Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", "https://fixture.openai.azure.com/");
        Environment.SetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME", "fixture-model");
        foreach (bool wait in new[] { true, false })
        {
            // The injected credential fails before any model request can reach the network.
            await using var host = await ApiSession.StartAsync(ApiSettings.Read("azure") with
                { RequestTimeoutSeconds = 1 }, new FixtureCredential(wait));
            using var client = new HttpClient { BaseAddress = new Uri(host.Address), Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.PostAsJsonAsync("/api/status", new { service = "vpn" });
            string raw = await response.Content.ReadAsStringAsync();
            using var failure = JsonDocument.Parse(raw);
            Lab.Require((int)response.StatusCode == (wait ? 504 : 502) &&
                failure.RootElement.GetProperty("phase").GetString() == "identity", "Credential failure boundary was lost.");
            Lab.Require(!raw.Contains("unit14-sensitive-marker"), "Exception details leaked to the caller.");
        }
        foreach (var scenario in new[] { (Fail: true, Synchronous: false), (Fail: false, Synchronous: false), (Fail: true, Synchronous: true) })
        {
            var credential = new NonCooperativeCredential(scenario.Synchronous);
            await using var host = await ApiSession.StartAsync(ApiSettings.Read("azure") with
                { RequestTimeoutSeconds = 1, Concurrency = 1 }, credential);
            using var client = new HttpClient { BaseAddress = new Uri(host.Address), Timeout = TimeSpan.FromSeconds(5) };
            try
            {
                var watch = Stopwatch.StartNew();
                using var response = await client.PostAsJsonAsync("/api/status", new { service = "vpn" });
                var failure = await response.Content.ReadFromJsonAsync<JsonElement>();
                Lab.Require(response.StatusCode == HttpStatusCode.GatewayTimeout && watch.Elapsed < TimeSpan.FromSeconds(3)
                    && failure.GetProperty("phase").GetString() == "identity", "An uncooperative credential defeated the HTTP deadline.");
                using var busy = await client.PostAsJsonAsync("/api/status", new { service = "email" });
                Lab.Require((int)busy.StatusCode == 429 && credential.Calls == 1, "A retry started work while a timed-out operation was still running.");
                var health = await client.GetFromJsonAsync<JsonElement>("/health/ready");
                Lab.Require(health.GetProperty("deadlinePolicy").GetString() == "bounded-wait-v1"
                    && health.GetProperty("activeOperations").GetInt32() == 1
                    && health.GetProperty("drainingOperations").GetInt32() == 1, "Detached work was not visible in health metadata.");
                credential.Complete(scenario.Fail);
                var drainWatch = Stopwatch.StartNew();
                do
                {
                    health = await client.GetFromJsonAsync<JsonElement>("/health/ready");
                    if (health.GetProperty("drainingOperations").GetInt32() == 0) break;
                    await Task.Delay(20);
                } while (drainWatch.Elapsed < TimeSpan.FromSeconds(3));
                Lab.Require(health.GetProperty("drainingOperations").GetInt32() == 0
                    && health.GetProperty("activeOperations").GetInt32() == 0, "The admission lease was not released after late completion.");
            }
            finally { credential.Complete(true); }
        }
    }
    finally
    {
        Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", originalEndpoint);
        Environment.SetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME", originalDeployment);
    }
    string? originalTimeout = Environment.GetEnvironmentVariable("Runner__RequestTimeoutSeconds");
    try
    {
        foreach (string invalid in new[] { "0", "181", "not-a-number" })
        {
            Environment.SetEnvironmentVariable("Runner__RequestTimeoutSeconds", invalid);
            bool rejected = false;
            try { ApiSettings.Read("fixture"); } catch (ArgumentException) { rejected = true; }
            Lab.Require(rejected, "Invalid request timeout was accepted.");
        }
    }
    finally { Environment.SetEnvironmentVariable("Runner__RequestTimeoutSeconds", originalTimeout); }
    await using (var host = await ApiSession.StartAsync(ApiSettings.Read("fixture") with { Concurrency = 1, FixtureDelayMs = 350 }))
    {
        using var client = new HttpClient { BaseAddress = new Uri(host.Address) };
        using var unknown = await client.PostAsJsonAsync("/api/status", new { service = "unassigned" });
        Lab.Require(unknown.StatusCode == HttpStatusCode.BadRequest, "Unknown service was accepted.");
        var pending = Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync("/api/status", new { service = "vpn" })).ToArray();
        var responses = await Task.WhenAll(pending);
        try
        {
            Lab.Require(responses.Any(r => (int)r.StatusCode == 429) && responses.Any(r => r.IsSuccessStatusCode), "Admission limit was not enforced.");
            Lab.Require(responses.Where(r => (int)r.StatusCode == 429).All(r => r.Headers.RetryAfter is not null), "Busy response has no Retry-After.");
        }
        finally { foreach (var response in responses) response.Dispose(); }
        var wrongMode = await HttpProbe.RunAsync(context, host.Address, context.Release, "azure", "mode-rejected.json", 1);
        Lab.Require(wrongMode.ExitCode == 1, "Fixture evidence was accepted as Azure evidence.");
    }
    using (var manifests = JsonDocument.Parse(JsonSerializer.Serialize(Kubernetes.Render("<APPROVED_IMAGE>", context.Release, true), Lab.Json)))
    {
        var deployment = manifests.RootElement.GetProperty("items")[1].GetProperty("spec");
        Lab.Require(!deployment.TryGetProperty("replicas", out _), "Fixed replicas conflict with HPA ownership.");
        Lab.Require(deployment.GetProperty("strategy").GetProperty("rollingUpdate").GetProperty("maxUnavailable").GetInt32() == 0, "Rolling availability contract changed.");
    }
    foreach (string invalid in new[] { "http://resource.openai.azure.com/", "https://user@resource.openai.azure.com/", "https://resource.openai.azure.com/path", "https://example.invalid/" })
    {
        bool rejected = false;
        try { EndpointPolicy.Validate(invalid); } catch (ArgumentException) { rejected = true; }
        Lab.Require(rejected, "Invalid model destination was accepted.");
    }
    string literal = Path.Combine(context.Artifacts, "literal.env");
    File.WriteAllText(literal, "UNIT14_LITERAL='$(not-executed)'\nUNIT14_PRECEDENCE=file\n");
    Environment.SetEnvironmentVariable("UNIT14_PRECEDENCE", "process");
    try
    {
        EnvironmentFile.Load(literal);
        Lab.Require(Environment.GetEnvironmentVariable("UNIT14_LITERAL") == "$(not-executed)", "Environment value was evaluated.");
        Lab.Require(Environment.GetEnvironmentVariable("UNIT14_PRECEDENCE") == "process", "Process precedence changed.");
    }
    finally
    {
        Environment.SetEnvironmentVariable("UNIT14_LITERAL", null);
        Environment.SetEnvironmentVariable("UNIT14_PRECEDENCE", null);
    }
    context.Save("checks.json", new { passed = true, checks = new[] { "HTTP unknown service", "bounded concurrency", "Retry-After", "mode separation", "HPA ownership", "endpoint boundaries", "literal environment", "process precedence", "request deadline", "identity timeout", "noncooperative identity deadline", "detached work admission", "late failure and completion cleanup", "sanitized failure diagnostics", "timeout configuration bounds" } });
});

public sealed class FixtureCredential(bool wait) : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => throw new AuthenticationFailedException("unit14-sensitive-marker");
    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        if (wait) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new AuthenticationFailedException("unit14-sensitive-marker");
    }
}

public sealed class NonCooperativeCredential(bool blockSynchronously) : TokenCredential
{
    private readonly TaskCompletionSource<AccessToken> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int calls;
    public int Calls => Volatile.Read(ref calls);
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => throw new AuthenticationFailedException("unit14-sensitive-marker");
    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        // Deliberately ignore cancellation to reproduce a dependency that does not stop.
        return blockSynchronously ? new(completion.Task.GetAwaiter().GetResult()) : new(completion.Task);
    }
    public void Complete(bool fail)
    {
        if (fail) completion.TrySetException(new AuthenticationFailedException("unit14-sensitive-marker"));
        else completion.TrySetResult(new AccessToken("unit14-fake-token", DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}
