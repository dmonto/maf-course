using Unit13;
using System.Text.Json;
using System.Security.Claims;

return await Entry.RunAsync("v03", args, context =>
{
    context.FixtureOnly();
    MemoryAuditSink audit = new();
    SecurityTools tools = new(() => Fixtures.Reader, "vpn", Guid.NewGuid().ToString("N"), audit);
    Evidence evidence = tools.GetServiceStatus("vpn");
    string payload = JsonSerializer.Serialize(evidence, Lab.Json);
    using JsonDocument document = JsonDocument.Parse(payload);
    string[] fields = document.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
    Lab.Require(fields.SequenceEqual(new[] { "service", "status", "summary" }), "Unexpected model-visible fields.");
    Lab.Require(!Privacy.HasEmail(payload), "Contact data was projected.");
    string masked = Privacy.MaskEmails("Contact user@example.invalid about vpn.");
    Lab.Require(masked.Contains("[email]", StringComparison.Ordinal) && !Privacy.HasEmail(masked), "Email masking failed.");
    context.Save("projection.json", new { fields, evidence, maskedInput = masked,
        limitation = "Email matching is a narrow demonstration, not a complete PII or secret detector." });
    Lab.Show(new { projectedFieldCount = fields.Length, contactExcluded = true });
    return Task.FromResult(0);
});
