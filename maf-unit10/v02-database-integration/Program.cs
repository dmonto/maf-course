return await Lab.RunAsync(args, "v02", context =>
{
    int count = context.Tools.GetOpenTicketCount(context.Service);
    // This second synthetic caller proves the tenant predicate against the same local file.
    var otherTenant = new Caller("training-b", "fixture-other-tenant", [context.Service], false);
    int otherCount = context.Tickets.CountOpen(otherTenant, context.Service);
    bool checkedIsolation = context.Service == "vpn";
    if (checkedIsolation) Output.Require(count == 2 && otherCount == 1, "SQL fixture isolation failed.");
    string query = "Fixed SELECT with tenant, service and open-status parameters.";

    // Readable summary; the RESULT JSON line printed afterwards stays last.
    Console.WriteLine($"Mode: {(context.Demo ? "local-demo" : "configured-live")}   Database: local SQLite   Model invoked: no");
    Console.WriteLine($"Service: {context.Service}   Caller tenant: {context.Caller.Tenant}");
    Console.WriteLine($"Query: {query}");
    Console.WriteLine("\nOpen tickets:");
    Console.WriteLine($"  {context.Caller.Tenant,-12} (caller)       {count}");
    Console.WriteLine($"  {otherTenant.Tenant,-12} (other tenant)  {otherCount}");
    Console.WriteLine(checkedIsolation
        ? "\nChecks:\n  PASS  Tenant predicate isolates tickets (expected 2 for caller, 1 for other tenant)\n"
        : "\nChecks:\n  SKIP  Isolation counts are only asserted for the vpn service\n");

    return Task.FromResult<object>(new { database = "local SQLite", service = context.Service,
        callerTenant = context.Caller.Tenant, openTickets = count, otherTenantOpenTickets = otherCount,
        query, modelInvoked = false });
});
