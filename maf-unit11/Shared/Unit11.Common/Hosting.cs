namespace Maf.Unit11;

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class LabHosting
{
    public static ILoggerFactory Logging() => LoggerFactory.Create(b => b.AddJsonConsole(o =>
    {
        o.IncludeScopes = true; o.TimestampFormat = "O"; o.UseUtcTimestamp = true;
    }));

    public static EventWorker Worker(ExampleContext ctx, EventStore store, IEventReasoner reasoner,
        ILogger<EventWorker> logger) => new(store, reasoner, logger, ctx.Workers, ctx.PollMs,
        ctx.BudgetSeconds, EnvironmentConfig.Number("MAX_RESULT_CHARS", 4000, 100, 16000));

    public static async Task<JobView[]> ProcessAsync(ExampleContext ctx, EventStore store,
        IEventReasoner reasoner, params string[] ids)
    {
        using var factory = Logging();
        using var worker = Worker(ctx, store, reasoner, factory.CreateLogger<EventWorker>());
        await worker.StartAsync(CancellationToken.None);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(ctx.BudgetSeconds * 3 + 15));
        try
        {
            while (true)
            {
                if (worker.ExecuteTask?.IsFaulted == true) await worker.ExecuteTask;
                JobView[] jobs = ids.Select(id => store.Read(id).Single()).ToArray();
                if (jobs.All(j => j.State is "Completed" or "Dead")) return jobs;
                await Task.Delay(25, deadline.Token);
            }
        }
        finally { await worker.StopAsync(CancellationToken.None); }
    }

    public static WebApplication Build(ExampleContext ctx, EventStore store,
        IEventReasoner reasoner, int defaultPort = 5111)
    {
        int port = ctx.Args.Number("port", defaultPort);
        if (port < 0 || port > 65535) throw new ArgumentException("--port must be between 0 and 65535.");
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 8192);
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(o =>
        {
            o.IncludeScopes = true; o.TimestampFormat = "O"; o.UseUtcTimestamp = true;
        });
        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(reasoner);
        builder.Services.AddHostedService(sp => Worker(ctx, store, reasoner,
            sp.GetRequiredService<ILogger<EventWorker>>()));
        var app = builder.Build();
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok", execution = ctx.Live ? "azure-maf" : "local-fixture",
            workers = ctx.Workers, database = store.Filename
        }));
        app.MapGet("/jobs", () => Results.Ok(store.Read()));
        app.MapGet("/jobs/{id}", (string id) => store.Read(id).FirstOrDefault() is { } job
            ? Results.Ok(job) : Results.NotFound());
        app.MapPost("/events", (SupportEvent e, HttpContext http) =>
        {
            try
            {
                bool created = store.Accept(e);
                return Results.Accepted($"/jobs/{e.Id}", new { eventId = e.Id, created, state = "Accepted" });
            }
            catch (CapacityException)
            {
                http.Response.Headers.RetryAfter = "2";
                return Results.Json(new { error = "Queue capacity reached; retry the same envelope." }, statusCode: 429);
            }
            catch (InvalidDataException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
        return app;
    }

    public static async Task ServeAsync(ExampleContext ctx, IIntakePolicy? policy = null, int port = 5111,
        EventStore? customStore = null)
    {
        var store = customStore ?? ctx.Store(policy);
        store.Initialize();
        await using var app = Build(ctx, store, ctx.Reasoner(), port);
        ConsoleResult.Write(new { status = "starting", example = ctx.Example,
            execution = ctx.Live ? "azure-maf" : "local-fixture", database = ctx.Database,
            url = $"http://127.0.0.1:{ctx.Args.Number("port", port)}" });
        await app.RunAsync();
    }

    public static async Task SubmitAsync(ExampleContext ctx, int defaultPort = 5111)
    {
        int port = ctx.Args.Number("port", defaultPort);
        string file = ctx.Args.Get("file", "");
        if (!File.Exists(file)) throw new ArgumentException("Use --file with a saved event JSON file.");
        byte[] bytes = await File.ReadAllBytesAsync(file);
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false });
        using var body = new ByteArrayContent(bytes);
        body.Headers.ContentType = new("application/json");
        using var response = await client.PostAsync($"http://127.0.0.1:{port}/events", body);
        string responseText = await response.Content.ReadAsStringAsync();
        ConsoleResult.Write(new { statusCode = (int)response.StatusCode, response = responseText });
        response.EnsureSuccessStatusCode();
    }
}

public static class StreamingExample
{
    public static async Task RunAsync(ExampleContext ctx)
    {
        ctx.Args.Allow("service", "delay-ms");
        SupportEvent e = ctx.Event("STREAM", ctx.Args.Get("service", "vpn"));
        e.Validate();
        var watch = Stopwatch.StartNew(); double? firstTextMs = null;
        var text = new StringBuilder(); int fragments = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ctx.BudgetSeconds));
        await ctx.Reasoner().RunAsync(e, 1, part =>
        {
            firstTextMs ??= watch.Elapsed.TotalMilliseconds;
            if (text.Length + part.Length > 4000) throw new InvalidDataException("Result bound exceeded.");
            text.Append(part); fragments++;
            Console.WriteLine("PROVISIONAL " + JsonSerializer.Serialize(new { sequence = fragments, text = part }, LabJson.Options));
        }, timeout.Token);
        ConsoleResult.Check(text.Length > 0, "The stream produced no text.");
        ConsoleResult.Write(new { status = "completed", example = ctx.Example,
            execution = ctx.Live ? "azure-maf" : "local-fixture", eventId = e.Id,
            firstTextMs, processingMs = watch.Elapsed.TotalMilliseconds, fragments,
            result = text.ToString(), actionExecuted = false });
    }
}

public sealed class TrackingReasoner(IEventReasoner inner) : IEventReasoner
{
    private int active, maximum;
    public int Maximum => maximum;
    public async Task RunAsync(SupportEvent e, int attempt, Action<string> onText, CancellationToken ct)
    {
        int current = Interlocked.Increment(ref active);
        int observed;
        do { observed = maximum; if (current <= observed) break; }
        while (Interlocked.CompareExchange(ref maximum, current, observed) != observed);
        try { await inner.RunAsync(e, attempt, onText, ct); }
        finally { Interlocked.Decrement(ref active); }
    }
}
