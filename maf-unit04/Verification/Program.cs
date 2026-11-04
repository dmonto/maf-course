using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Unit04.Common;
using Unit04.Exercise02;

Environment.SetEnvironmentVariable("Agent__PromptVersion", "v1");
Environment.SetEnvironmentVariable("MAX_CONVERSATION_TURNS", "6");
Environment.SetEnvironmentVariable("MAX_RETAINED_CONVERSATIONS", "20");
Environment.SetEnvironmentVariable("CONVERSATION_IDLE_SECONDS", "60");
int checks = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new InvalidOperationException("FAILED: " + name);
    checks++;
}
async Task<JsonElement> Json(HttpResponseMessage response, int expected)
{
    string body = await response.Content.ReadAsStringAsync();
    Check((int)response.StatusCode == expected, $"HTTP {expected}; received {(int)response.StatusCode}: {body[..Math.Min(body.Length, 250)]}");
    return JsonDocument.Parse(body).RootElement.Clone();
}
async Task<(WebApplication App, HttpClient Client)> Start(LabProfile profile, string id,
    Action<IServiceCollection>? extra = null)
{
    var app = LabHost.Build(["--demo", "--port", "0"], profile, id, extra);
    await app.StartAsync();
    return (app, new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(15) });
}
async Task<JsonElement> Chat(HttpClient client, string message, string? conversation = null, int expected = 200)
    => await Json(await client.PostAsJsonAsync("/api/chat", new { message, conversationId = conversation }), expected);
string Id(JsonElement reply) => reply.GetProperty("conversationId").GetString()!;
bool Evidence(JsonElement reply, string service, string status) => reply.GetProperty("evidence").EnumerateArray()
    .Any(e => e.GetProperty("ok").GetBoolean() && e.GetProperty("data").GetProperty("service").GetString() == service
        && e.GetProperty("data").GetProperty("status").GetString() == status);

// Exercise every section profile; no Azure credential or network inference is used.
for (int i = 1; i <= 10; i++)
{
    var profile = i == 1 ? LabProfile.ConfigurationOnly : i == 2 ? LabProfile.Registration : i == 3 ? LabProfile.Tools : LabProfile.Full;
    var (app, client) = await Start(profile, $"v{i:00}");
    await using (app)
    using (client)
    {
        var health = await Json(await client.GetAsync("/health"), 200);
        Check(health.GetProperty("example").GetString() == $"v{i:00}", "correct section profile");
        Check(health.GetProperty("executionMode").GetString() == "demo-scripted-model", "demo label");
        if (i == 1) Check((await client.PostAsJsonAsync("/api/chat", new { message = "Payroll" })).StatusCode == HttpStatusCode.NotFound, "configuration only has no chat");
        else if (i == 2)
        {
            var reply = await Chat(client, "Explain your capabilities.");
            Check(reply.GetProperty("evidence").GetArrayLength() == 0, "registration profile has no tool");
        }
        else Check(Evidence(await Chat(client, "What is the current status of payroll?"), "payroll", "degraded"), "registered real MAF tool invocation");
        if (i >= 4) Check((await client.GetAsync("/devui")).IsSuccessStatusCode, "DevUI responds");
        await app.StopAsync();
    }
}
{
    var probe = new ProbeClient();
    var (app, client) = await Start(LabProfile.Full, "verification", s => s.AddSingleton<IChatClient>(probe));
    await using (app)
    using (client)
    {
        await Chat(client, "", expected: 400);
        await Chat(client, "Payroll", "bad-id", 400);
        await Chat(client, "Payroll", "00000000000000000000000000000000", 404);
        var first = await Chat(client, "What is the current status of payroll?");
        string id = Id(first);
        Check(Evidence(first, "payroll", "degraded"), "payroll evidence");
        var follow = await Chat(client, "What about its recovery time?", id);
        Check(Evidence(follow, "payroll", "degraded"), "follow-up uses same session subject");
        var fresh = await Chat(client, "What is its current status?");
        Check(Id(fresh) != id && fresh.GetProperty("evidence").GetArrayLength() == 0, "fresh session has no leaked subject");
        foreach (string service in new[] { "CRM", "billing" })
        {
            var missing = await Chat(client, "What is the current status of " + service + "?");
            var e = missing.GetProperty("evidence")[0];
            Check(!e.GetProperty("ok").GetBoolean() && e.GetProperty("code").GetString() == "not-available" && e.GetProperty("data").ValueKind == JsonValueKind.Null, "controlled unavailable outcome");
        }
        var unsupported = await Chat(client, "Restart payroll now.");
        Check(unsupported.GetProperty("evidence").GetArrayLength() == 0 && unsupported.GetProperty("answer").GetString()!.Contains("cannot"), "no synthetic write capability");
        for (int turn = 3; turn <= 6; turn++) await Chat(client, "What is the current status of payroll?", id);
        var limited = await Chat(client, "Payroll again", id, 409);
        Check(limited.GetProperty("code").GetString() == "turn-limit", "six-turn limit");
        string concurrentId = Id(await Chat(client, "Payroll status"));
        var slow = Chat(client, "slow payroll status", concurrentId);
        await probe.SlowEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var busy = await Chat(client, "Payroll status", concurrentId, 409);
        Check(busy.GetProperty("code").GetString() == "conversation-busy", "overlapping turn refused");
        Check(Evidence(await Chat(client, "VPN status"), "vpn", "healthy"), "unrelated conversation proceeds");
        probe.ReleaseSlow.TrySetResult();
        await slow;
        string failedId = Id(await Chat(client, "Payroll status"));
        await Chat(client, "simulate-failure", failedId, 502);
        await Chat(client, "Payroll status", failedId, 404);
        // The dynamic Responses endpoint and streaming adapter are actually exercised.
        var protocol = await client.PostAsJsonAsync("/v1/responses", new { agent = new { name = "service-desk" }, input = "What is the current status of VPN?", stream = false });
        var protocolJson = await Json(protocol, 200);
        Check(protocolJson.GetRawText().Contains("healthy"), "protocol response contains grounded VPN answer");
        var streamed = await client.PostAsJsonAsync("/v1/responses", new { agent = new { name = "service-desk" }, input = "What is the current status of payroll?", stream = true });
        string events = await streamed.Content.ReadAsStringAsync();
        Check(streamed.IsSuccessStatusCode && events.Contains("response.completed") && events.Contains("degraded"), "streamed protocol response completes");
        await app.StopAsync();
    }
}
{
    var (app, client) = await Start(LabProfile.Full, "capacity");
    await using (app)
    using (client)
    {
        for (int n = 0; n < 20; n++) await Chat(client, "Which service should I specify?");
        var full = await Chat(client, "Which service?", expected: 429);
        Check(full.GetProperty("code").GetString() == "conversation-capacity", "baseline capacity stays bounded");
        await app.StopAsync();
    }
}
{
    var clock = new ManualClock();
    var probe = new ProbeClient();
    var (app, client) = await Start(LabProfile.Full, "expiry", services =>
    {
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IChatClient>(probe);
        services.AddSingleton<IChatHandler, ExpiringAgentChat>();
    });
    await using (app)
    using (client)
    {
        string oldId = Id(await Chat(client, "Payroll status"));
        await Chat(client, "What about its recovery time?", oldId);
        clock.Advance(61);
        var expired = await Chat(client, "Payroll status", oldId, 410);
        Check(expired.GetProperty("code").GetString() == "conversation-expired", "stable expiry outcome");
        string activeId = Id(await Chat(client, "Payroll status"));
        var active = Chat(client, "slow payroll status", activeId);
        await probe.SlowEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        clock.Advance(61);
        await Chat(client, "VPN status"); // Triggers cleanup while another session is active.
        await Chat(client, "Payroll status", activeId, 409);
        probe.ReleaseSlow.TrySetResult();
        await active;
        await Chat(client, "What about its recovery time?", activeId);
        Check(true, "cleanup cannot expire active turn; success updates idle clock");
        clock.Advance(61);
        for (int n = 0; n < 20; n++) await Chat(client, "Which service?");
        await Chat(client, "Which service?", expected: 429);
        clock.Advance(61);
        await Chat(client, "VPN status");
        Check(true, "expired sessions release capacity");
        await app.StopAsync();
    }
}
// Test startup validation without binding a listener.
Environment.SetEnvironmentVariable("CONVERSATION_IDLE_SECONDS", "0");
try
{
    await using var bad = LabHost.Build(["--demo", "--port", "0"], LabProfile.Full, "invalid-expiry",
        s => s.AddSingleton<IChatHandler, ExpiringAgentChat>());
    throw new InvalidOperationException("Invalid expiry was accepted.");
}
catch (ConfigurationException) { Check(true, "invalid expiry refused at startup"); }
Environment.SetEnvironmentVariable("CONVERSATION_IDLE_SECONDS", "60");
Console.WriteLine($"PASS: {checks} offline checks. These checks do not evaluate a live model's quality or Azure authentication.");

internal sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
}
internal sealed class ProbeClient : IChatClient
{
    private readonly ScriptedChatClient _inner = new();
    public TaskCompletionSource SlowEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseSlow { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages.ToArray();
        string message = list.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        if (message == "simulate-failure") throw new InvalidOperationException("Synthetic test failure.");
        if (message.StartsWith("slow"))
        {
            SlowEntered.TrySetResult();
            await ReleaseSlow.Task.WaitAsync(cancellationToken);
        }
        return await _inner.GetResponseAsync(list, options, cancellationToken);
    }
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => _inner.GetStreamingResponseAsync(messages, options, cancellationToken);
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() => _inner.Dispose();
}
