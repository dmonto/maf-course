return await Runner.RunAsync("v05", args, async c =>
{
    var employee = await c.SearchAsync("employee", "hybrid", "Payroll batch retry procedure");
    var payroll = await c.SearchAsync("payroll", "hybrid", "Payroll batch retry procedure");
    var tenantA = await c.SearchAsync("employee", "keyword", "BRAVO-CANARY");
    var tenantB = await c.SearchAsync("other-tenant", "keyword", "BRAVO-CANARY");
    var none = await c.SearchAsync("none", "hybrid", "VPN error 809");
    Output.Require(!employee.Any(h => h.DocumentId == "payroll-guide"), "Restricted payroll evidence leaked.");
    Output.Require(tenantA.Length == 0 && none.Length == 0, "Denied scope returned evidence.");
    Output.Result(new { executionMode = c.ExecutionMode, employee, payroll, tenantA, tenantB, none,
        isolationPassed = true, payrollVisible = payroll.Any(h => h.DocumentId == "payroll-guide") });
});
