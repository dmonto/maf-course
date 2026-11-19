return await Runner.RunAsync("v05", args, async c =>
{
    var employee = await c.SearchAsync("employee", "hybrid", "Payroll batch retry procedure");
    var payroll = await c.SearchAsync("payroll", "hybrid", "Payroll batch retry procedure");
    var tenantA = await c.SearchAsync("employee", "keyword", "BRAVO-CANARY");
    var tenantB = await c.SearchAsync("other-tenant", "keyword", "BRAVO-CANARY");
    var none = await c.SearchAsync("none", "hybrid", "VPN error 809");
    Output.Require(!employee.Any(h => h.DocumentId == "payroll-guide"), "Restricted payroll evidence leaked.");
    Output.Require(tenantA.Length == 0 && none.Length == 0, "Denied scope returned evidence.");
    bool payrollVisible = payroll.Any(h => h.DocumentId == "payroll-guide");

    // Readable summary; the RESULT JSON line below stays last.
    Console.WriteLine($"Execution mode: {c.ExecutionMode}\n");
    static void Show(string profile, string mode, string query, SearchHit[] hits)
    {
        Console.WriteLine($"== profile '{profile}', {mode} search \"{query}\": {hits.Length} hit(s) ==");
        if (hits.Length == 0) Console.WriteLine("    (no evidence visible)");
        foreach (var h in hits)
            Console.WriteLine($"    - {h.Title} ({h.DocumentId}, tenant {h.TenantId})");
    }
    Show("employee", "hybrid", "Payroll batch retry procedure", employee);
    Show("payroll", "hybrid", "Payroll batch retry procedure", payroll);
    Show("employee", "keyword", "BRAVO-CANARY", tenantA);
    Show("other-tenant", "keyword", "BRAVO-CANARY", tenantB);
    Show("none", "hybrid", "VPN error 809", none);
    Console.WriteLine("\nChecks:");
    Console.WriteLine("  PASS  Employee cannot see payroll-guide");
    Console.WriteLine("  PASS  Tenant A and empty scope return no evidence");
    Console.WriteLine($"  {(payrollVisible ? "PASS" : "FAIL")}  Payroll profile sees payroll-guide\n");

    Output.Result(new { executionMode = c.ExecutionMode, employee, payroll, tenantA, tenantB, none,
        isolationPassed = true, payrollVisible });
});
