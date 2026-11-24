using Unit13;
using System.Text.Json;
using System.Security.Claims;

return await Entry.RunAsync("v05", args, context =>
{
    context.FixtureOnly();
    int days = int.Parse(Environment.GetEnvironmentVariable("SECURITY_RETENTION_DAYS") ?? "30");
    Lab.Require(days is >= 1 and <= 3650, "Retention duration must be between 1 and 3650 days.");
    DateTimeOffset now = DateTimeOffset.UtcNow;
    string owned = Path.Combine(context.Artifacts, "owned-retention-store");
    Directory.CreateDirectory(owned);
    RetainedItem[] items =
    [
        new("expired-session.json", "conversation", now.AddDays(-days), false),
        new("held-audit.json", "audit", now.AddDays(-days), true),
        new("current-evaluation.json", "evaluation", now.AddDays(days), false)
    ];
    foreach (RetainedItem item in items) File.WriteAllText(Path.Combine(owned, item.FileName), "{\"synthetic\":true}");
    RetentionResult[] results = Retention.ApplyToOwnedLabFiles(owned, items, now);
    Lab.Require(!File.Exists(Path.Combine(owned, "expired-session.json")), "Expired owned file survived.");
    Lab.Require(File.Exists(Path.Combine(owned, "held-audit.json")), "Held evidence was deleted.");
    Lab.Require(File.Exists(Path.Combine(owned, "current-evaluation.json")), "Current evidence was deleted.");
    context.Save("retention.json", new { illustrativeDays = days, actualClock = now, items, results,
        scope = "Only the three synthetic files created inside this run are managed." });
    Lab.Show(new { expiredDeleted = true, legalHoldPreserved = true, currentPreserved = true });
    return Task.FromResult(0);
});
