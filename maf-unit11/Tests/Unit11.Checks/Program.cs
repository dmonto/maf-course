using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Maf.Unit11;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;

return await ConsoleResult.Run("checks", args, async ignored =>
{
    var passed = new List<string>();
    void Pass(string name) { passed.Add(name); Console.WriteLine("PASS " + name); }
    ExampleContext Context()
    {
        string database = Path.Combine(EnvironmentConfig.Root, ".data", "checks", Guid.NewGuid().ToString("N"), "queue.db");
        return new ExampleContext("checks", new Arguments(["--demo", "--database", database, "--port", "0"]));
    }
    SupportEvent Event(ExampleContext ctx) => ctx.Event("CHECK");
    void Expire(EventStore store, string id)
    {
        using var connection = new SqliteConnection($"Data Source={store.Filename}"); connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE jobs SET lease_until_ms=0 WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery();
    }

    var ctx = Context(); var store = ctx.Store(); var e = Event(ctx);
    var deliveries = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => store.Accept(e))));
    ConsoleResult.Check(deliveries.Count(x => x) == 1, "Concurrent duplicates created more than one job.");
    bool conflict = false;
    try { store.Accept(e with { Data = e.Data with { Status = "healthy" } }); }
    catch (InvalidDataException) { conflict = true; }
    ConsoleResult.Check(conflict, "Changed payload reused the same identity.");
    Pass("Concurrent duplicate suppression and payload conflict");
    Claim old = store.TryClaim()!; Expire(store, e.Id);
    Claim current = store.TryClaim()!;
    ConsoleResult.Check(current.Attempt == 2 && old.Owner != current.Owner &&
        !store.Complete(old, "stale", null, 0) && store.Complete(current, "winner", null, 1),
        "Expired ownership was not fenced.");
    var reopened = new EventStore(ctx.Database); reopened.Initialize();
    ConsoleResult.Check(reopened.Read(e.Id).Single().Result == "winner" && !reopened.Accept(e), "Persistence failed.");
    Pass("Expired claim ownership and durable completion");
    var abandoned = Event(ctx); store.Accept(abandoned);
    for (int i = 1; i <= 3; i++)
    {
        ConsoleResult.Check(store.TryClaim()!.Attempt == i, "Wrong recovery attempt."); Expire(store, abandoned.Id);
    }
    ConsoleResult.Check(store.TryClaim() is null && store.Read(abandoned.Id).Single().State == "Dead", "Final abandoned attempt was not quarantined.");
    Pass("Abandoned final attempt is recoverable and bounded");

    ctx = Context(); store = ctx.Store(); e = Event(ctx); store.Accept(e);
    var failed = (await LabHosting.ProcessAsync(ctx, store, new AlwaysTransient(), e.Id)).Single();
    ConsoleResult.Check(failed.State == "Dead" && failed.Attempts == 3 && failed.Result is null, "Transient retry limit failed.");
    Pass("Repeated transport failure stops at three attempts");

    ctx = Context(); store = ctx.Store(); e = Event(ctx); store.Accept(e);
    var oversized = (await LabHosting.ProcessAsync(ctx, store, new OversizedReasoner(), e.Id)).Single();
    ConsoleResult.Check(oversized.State == "Dead" && oversized.Attempts == 1 && oversized.Result is null, "Oversized output became a final result.");
    Pass("Result bound quarantines incomplete output");

    ctx = Context(); store = ctx.Store();
    await using (var app = LabHosting.Build(ctx, store, new FixtureReasoner(delayMs: 25)))
    {
        await app.StartAsync();
        using var http = Client(app);
        var input = Event(ctx);
        using var accepted = await http.PostAsJsonAsync("/events", input, LabJson.Options);
        ConsoleResult.Check(accepted.StatusCode == HttpStatusCode.Accepted, "HTTP intake failed.");
        var completed = await Wait(http, input.Id, "Completed");
        using var duplicate = await http.PostAsJsonAsync("/events", input, LabJson.Options);
        using var duplicateJson = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        ConsoleResult.Check(duplicate.StatusCode == HttpStatusCode.Accepted &&
            !duplicateJson.RootElement.GetProperty("created").GetBoolean(), "Duplicate HTTP acceptance failed.");
        foreach (SupportEvent invalid in new[] { Event(ctx) with { Tenant = "training-b" },
            Event(ctx) with { Type = "Unknown.v1" }, Event(ctx) with { OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(1) } })
        {
            using var bad = await http.PostAsJsonAsync("/events", invalid, LabJson.Options);
            using var absent = await http.GetAsync("/jobs/" + invalid.Id);
            ConsoleResult.Check(bad.StatusCode == HttpStatusCode.BadRequest && absent.StatusCode == HttpStatusCode.NotFound,
                "Invalid intake created work.");
        }
        using var tooBig = await http.PostAsJsonAsync("/events", Event(ctx) with
            { Data = new("vpn", "degraded", new string('x', 9000)) }, LabJson.Options);
        ConsoleResult.Check(tooBig.StatusCode == HttpStatusCode.RequestEntityTooLarge, "Body bound failed.");
        using var unknown = await http.PostAsJsonAsync("/events", new { extra = true }, LabJson.Options);
        ConsoleResult.Check(unknown.StatusCode == HttpStatusCode.BadRequest, "Unknown fields were accepted.");
        ConsoleResult.Check(completed.Attempts == 1, "Unexpected generation attempt count.");
        await app.StopAsync();
    }
    Pass("HTTP acceptance, asynchronous completion, duplicates, scope and size boundaries");

    ConsoleResult.Write(new { status = "passed", checks = passed.Count, passed, azureCalls = 0, serviceBusCalls = 0 });
});

static HttpClient Client(WebApplication app) => new(new HttpClientHandler { UseProxy = false })
{ BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(30) };

static async Task<JobView> Wait(HttpClient client, string id, string state)
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    while (true)
    {
        var job = await client.GetFromJsonAsync<JobView>("/jobs/" + id, LabJson.Options, deadline.Token)
            ?? throw new InvalidDataException("Missing job.");
        if (job.State == state) return job;
        if (job.State == "Dead") throw new InvalidOperationException("Unexpected Dead state: " + job.ErrorCode);
        await Task.Delay(20, deadline.Token);
    }
}

sealed class AlwaysTransient : IEventReasoner
{
    public Task RunAsync(SupportEvent e, int attempt, Action<string> onText, CancellationToken ct)
        => throw new HttpRequestException("Controlled transport failure.");
}
sealed class OversizedReasoner : IEventReasoner
{
    public Task RunAsync(SupportEvent e, int attempt, Action<string> onText, CancellationToken ct)
    { onText(new string('x', 5000)); return Task.CompletedTask; }
}
sealed class GateReasoner : IEventReasoner
{
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Release() => ready.TrySetResult();
    public async Task RunAsync(SupportEvent e, int attempt, Action<string> onText, CancellationToken ct)
    { await ready.Task.WaitAsync(ct); onText("Completed synthetic event for " + e.Data.Service); }
}
