using System.Globalization;
return await Runner.RunAsync("ex01", args, async c =>
{
    var current = c.Documents.Select(d => d with { ValidUntil = DateTimeOffset.UtcNow.AddHours(1) }).ToArray();
    var expired = current.Select(d => d.DocumentId == "vpn-guide" ? d with { ValidUntil = DateTimeOffset.UtcNow.AddHours(-1) } : d).ToArray();
    await c.Store().SeedAsync(expired, c.Token);
    // Search may lag writes in Azure. Poll for the policy outcome with a finite deadline.
    async Task<SearchHit[]> AwaitVpn(bool expected)
    {
        for (int i = 0; i < 12; i++)
        {
            var hits = await c.SearchAsync("employee", "hybrid", "VPN error 809");
            if (hits.Any(h => h.DocumentId == "vpn-guide") == expected) return hits;
            await Task.Delay(1000, c.Token);
        }
        throw new InvalidOperationException("Expiry update did not become visible before the deadline.");
    }
    var before = await AwaitVpn(false);
    var payroll = await c.SearchAsync("payroll", "hybrid", "Payroll batch retry procedure");
    Output.Require(payroll.Any(h => h.DocumentId == "payroll-guide"), "Valid permitted payroll guidance is missing.");
    await c.Store().SeedAsync(current, c.Token);
    var after = await AwaitVpn(true);
    var employee = await c.SearchAsync("employee", "hybrid", "Payroll batch retry procedure");
    Output.Require(!employee.Any(h => h.DocumentId == "payroll-guide"), "Expiry policy weakened the ACL.");
    var missing = current.Select(d => d.DocumentId == "vpn-guide" ? d with { ValidUntil = null } : d).ToArray();
    await c.Store().SeedAsync(missing, c.Token);
    var absentExpiry = await AwaitVpn(false);
    // Restore a valid fixture after all three checks.
    await c.Store().SeedAsync(current, c.Token);
    Output.Result(new { executionMode = c.ExecutionMode, expiredVpnExcluded = true, futureVpnReturned = true,
        missingExpiryExcluded = true, restrictedPayrollExcluded = true, before, after, absentExpiry });
}, new ExpiryPolicy());
internal sealed class ExpiryPolicy : IIndexExtension
{
    public IEnumerable<object> Fields() => [new { name = "valid_until", type = "Edm.DateTimeOffset", filterable = true, retrievable = false }];
    public void AddValues(KnowledgeDocument document, Dictionary<string, object> values) =>
        values["valid_until"] = document.ValidUntil is { } value ? value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : null!;
    public string FilterSuffix(DateTimeOffset now) => " and valid_until gt " + now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
