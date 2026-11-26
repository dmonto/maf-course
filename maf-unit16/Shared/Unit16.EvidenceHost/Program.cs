using Unit16.Common;

UnitSettings.Load();
var builder = WebApplication.CreateBuilder(args);
string mode = builder.Configuration["Runner:Mode"] ?? "fixture";
if (mode is not ("fixture" or "azure")) throw new InvalidOperationException("Runner__Mode must be fixture or azure.");
string identity = builder.Configuration["Runner:Identity"] ?? "cli";
if (mode == "azure" && identity != "cli")
    throw new InvalidOperationException("This local host uses Runner__Identity=cli. Use the deployed Unit 14 host for managed identity.");
string release = builder.Configuration["Runner:Release"] ?? "unit16-csharp";
if (mode == "azure")
{
    _ = UnitSettings.Required("AZURE_OPENAI_ENDPOINT");
    _ = UnitSettings.Required("AZURE_OPENAI_DEPLOYMENT_NAME");
}
using SemaphoreSlim capacity = new(2, 2);
var app = builder.Build();
app.MapGet("/health/ready", () =>
{
    LabConsole.Write("HTTP IN", "GET /health/ready => 200. No model call is performed.");
    return Results.Json(new { status = "ok", mode, release, promptVersion = "migration-v2" });
});
app.MapGet("/health/live", () => Results.Json(new { status = "ok" }));
app.MapPost("/api/status", async Task<IResult> (StatusRequest input, HttpContext context) =>
{
    LabConsole.Write("HTTP IN", "POST /api/status");
    string service;
    try { service = Contract.Normalize(input.Service ?? ""); }
    catch (ArgumentException) { return Results.BadRequest(new { error = "invalid_service" }); }
    LabConsole.Data("HTTP REQUEST BODY", new { service });
    if (!Contract.Catalog.ContainsKey(service))
    {
        LabConsole.Write("HTTP OUT", "400 unknown_service; MAF is not invoked.");
        return Results.BadRequest(new { error = "unknown_service" });
    }
    if (!await capacity.WaitAsync(0, context.RequestAborted))
    {
        context.Response.Headers.RetryAfter = "1";
        LabConsole.Write("HTTP OUT", "429 busy; bounded concurrency=2.");
        return Results.Json(new { error = "busy" }, statusCode: 429);
    }
    try
    {
        using MafRuntime runtime = new(mode == "azure");
        CandidateResult result = await runtime.ReadAsync(service, context.RequestAborted);
        LabConsole.Write("HTTP OUT", "200; one actual MAF tool read; contract matches evidence.");
        return Results.Json(new { result.Evidence.Service, result.Evidence.Status, result.Evidence.Summary,
            mode, release, promptVersion = "migration-v2", toolTrace = result.Calls });
    }
    catch (OperationCanceledException)
    { return Results.Json(new { error = "request_cancelled" }, statusCode: 504); }
    catch (Exception ex)
    {
        LabConsole.Write("HTTP ERROR", ex.GetType().Name);
        return Results.Json(new { error = "evidence_contract_failed" }, statusCode: 502);
    }
    finally { capacity.Release(); }
});
LabConsole.Write("HOST", $"Mode={mode}; identity={identity}; release={release}; route=POST /api/status.");
LabConsole.Write("MILESTONE", "Host configured. Use GET /health/ready and POST /api/status from a second terminal.");
await app.RunAsync();

internal sealed record StatusRequest(string? Service);
