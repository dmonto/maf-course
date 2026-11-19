using System.Net;
using System.Text.Json;
using Microsoft.Data.Sqlite;

return await Lab.RunAsync(args, "verify", async parent =>
{
    Output.Require(parent.Demo, "Contract verification uses the local dependency fixture.");
    using var context = new LabContext(parent.Root, "verify", new Options(["--demo"]), Guid.NewGuid().ToString("N"));
    var checks = new List<string>();
    void Check(bool value, string label) { Output.Require(value, label); checks.Add(label); }
    Check(context.Tools.GetOpenTicketCount("vpn") == 2, "tenant-and-open-status-sql-predicates");
    Check(context.Tickets.CountOpen(new("training-b", "fixture", ["vpn"], false), "vpn") == 1, "cross-tenant-test-record-is-present");
    bool denied = false;
    try { context.Tools.GetOpenTicketCount("vpn' OR 1=1--"); } catch (UnauthorizedAccessException) { denied = true; }
    Check(denied, "sql-shaped-service-is-denied");
    foreach (var tool in AgentHost.ReadTools(context.Tools))
        Check(tool.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).SequenceEqual(["service"]),
            "real-function-schema-hides-runtime-scope-" + tool.Name);
    object inspection = await AgentHost.InspectAsync(context);
    Check(JsonSerializer.Serialize(inspection, Output.Json).Contains("\"modelInvoked\":false"), "demo-is-labelled-without-model-inference");
    var draft = await context.PrepareAsync(default);
    await context.Sender().SendAsync(draft, default);
    var captured = context.Fixture!.Requests.Last(x => x.Method == "POST");
    using (var body = JsonDocument.Parse(captured.Body!))
        Check(body.RootElement.GetProperty("channel").GetString() == draft.Channel &&
            body.RootElement.GetProperty("text").GetString() == draft.Text &&
            !body.RootElement.GetProperty("mrkdwn").GetBoolean() &&
            !body.RootElement.GetProperty("unfurl_links").GetBoolean(), "slack-payload-equals-preview-with-markup-disabled");
    Check(captured.Authorized, "host-attaches-bearer-credential");
    var claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => context.Tickets.TryClaim("race", "hash"))));
    Check(claims.Count(x => x) == 1, "one-owner-for-concurrent-delivery-claims");
    context.Tickets.Complete("race", "sent", "fixture-receipt");
    Check(new TicketStore(context.DatabaseFile).State("race") == "sent", "state-survives-reopening");
    var result = await NotificationFlow.ExecuteAsync(context, "expired-preview",
        prepare: _ => Task.FromResult(draft with { PreparedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3) }),
        review: (_, _) => Task.FromResult(false));
    Check(result.State == "cancelled", "unapproved-action-stops-before-claim");
    bool expired = false;
    try
    {
        await NotificationFlow.ExecuteAsync(context, "expired-approved",
            prepare: _ => Task.FromResult(draft with { PreparedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3) }),
            review: (_, _) => Task.FromResult(true));
    }
    catch (InvalidOperationException) { expired = true; }
    Check(expired && context.Tickets.State(NotificationFlow.Key(context.Caller, "expired-approved")) == "absent",
        "expired-approval-produces-no-delivery-claim");
    context.Fixture.Case = "internal";
    var unknown = await NotificationFlow.ExecuteAsync(context, "uncertain-send", review: (_, _) => Task.FromResult(true));
    int posts = context.Fixture.Requests.Count(x => x.Method == "POST");
    context.Fixture.Case = "normal";
    var replay = await NotificationFlow.ExecuteAsync(context, "uncertain-send", review: (_, _) => Task.FromResult(true));
    Check(unknown.State == "unknown" && replay.State == "already-unknown" &&
        context.Fixture.Requests.Count(x => x.Method == "POST") == posts, "uncertain-send-is-not-automatically-retried");
    foreach (string fault in new[] { "legacy-code", "legacy-flag" })
    {
        context.Fixture.Case = fault;
        bool rejected = false;
        try { await new LegacyStatusAdapter(context.Caller, context.Http, context.ApiRoot, context.ApiToken).ReadAsync("vpn", default); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected, "invalid-legacy-contract-" + fault);
    }
    // A different payload must not reuse a stable operation key to trigger another send.
    Check(context.Tickets.TryClaim("same-key", "first") && !context.Tickets.TryClaim("same-key", "different"),
        "changed-payload-cannot-bypass-operation-identity");
    return new { passed = checks.Count, checks, externalRequests = 0, modelInvoked = false };
});
