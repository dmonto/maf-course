using System.Globalization;
using System.Text.Json;

internal static class FixtureServer
{
    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();
        var app = builder.Build();
        string token = LabConfig.Required("CORPORATE_API_TOKEN");
        string statusFault = Environment.GetEnvironmentVariable("FIXTURE_STATUS_FAULT") ?? "none";
        string slackFault = Environment.GetEnvironmentVariable("FIXTURE_SLACK_FAULT") ?? "none";
        bool Authorized(HttpRequest request) => request.Headers.Authorization == $"Bearer {token}";
        app.MapGet("/services/{service}", (string service, HttpRequest request) =>
        {
            if (!Authorized(request)) return Results.Unauthorized();
            if (service is not ("vpn" or "erp" or "payroll")) return Results.NotFound();
            if (statusFault == "forbidden") return Results.StatusCode(403);
            if (statusFault == "unavailable") return Results.StatusCode(503);
            var time = DateTimeOffset.UtcNow;
            return Results.Json(new ServiceStatus(service,
                statusFault == "tenant" ? "training-b" : "training-a",
                statusFault == "invalid" ? "probably-okay" : service == "vpn" ? "degraded" : "healthy",
                statusFault == "stale" ? time.AddMinutes(-10) : time,
                "Synthetic fixture evidence; no estimated recovery time."));
        });
        app.MapGet("/legacy/status/{service}", (string service, HttpRequest request) =>
        {
            if (!Authorized(request)) return Results.Unauthorized();
            if (service is not ("vpn" or "erp" or "payroll")) return Results.NotFound();
            return Results.Json(new LegacyStatus(LegacyStatusAdapter.Code(service), "training-a",
                service == "vpn" ? "D" : "H", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        });
        app.MapPost("/slack/chat.postMessage", async (HttpRequest request, HttpResponse response) =>
        {
            if (!Authorized(request)) return Results.Unauthorized();
            if (slackFault == "rate")
            {
                response.Headers.RetryAfter = "2";
                return Results.StatusCode(429);
            }
            if (slackFault == "scope") return Results.Json(new { ok = false, error = "missing_scope" });
            using var payload = await JsonDocument.ParseAsync(request.Body);
            string? channel = payload.RootElement.GetProperty("channel").GetString();
            if (channel != "C000TRAINING") return Results.Json(new { ok = false, error = "channel_not_found" });
            string ts = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000m)
                .ToString("0.000", CultureInfo.InvariantCulture);
            Console.WriteLine($"Fixture received notification in {channel}; receipt={ts}");
            return Results.Json(new { ok = true, channel, ts });
        });
        Console.WriteLine("Synthetic REST and Slack fixtures: http://127.0.0.1:5098");
        await app.RunAsync("http://127.0.0.1:5098");
    }
}
