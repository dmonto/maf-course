using System.Text.Json;
using Microsoft.Extensions.AI;
using Unit03.Common;
using Unit03.Exercises.TrustBoundaries;
using Unit03.Exercises.SupportContact;

List<string> checks = [];
return await CourseApp.RunAsync(["--demo"], async context =>
{
    var audit = new Audit(TextWriter.Null);
    var repository = new StatusRepository();
    var runtime = new ToolRuntime(ActorContext.Student, 3, audit);
    var tools = new ServiceTools(repository, runtime);
    var function = AIFunctionFactory.Create(tools.GetServiceStatus);
    var properties = function.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray();
    Check(properties.SequenceEqual(["serviceName"]), "tool schema excludes identity, permissions and cancellation token");
    runtime.BeginTurn("allowed");
    await Invoke(function, " Payroll ");
    Check(runtime.Evidence[^1].Result.Data is ServiceStatus { Service: "payroll", Status: "degraded" }, "registered function normalizes and retrieves permitted evidence");
    int before = repository.Reads;
    runtime.BeginTurn("denied");
    await Invoke(function, "crm");
    Check(repository.Reads == before && runtime.Evidence[^1].Result is { Ok: false, Code: "not-available", Data: null }, "permission check blocks repository access and record data");
    runtime.BeginTurn("missing");
    await Invoke(function, "billing");
    Check(runtime.Evidence[^1].Result is { Code: "not-available", Data: null }, "missing record and inaccessible record share the public failure shape");
    before = repository.Reads;
    runtime.BeginTurn("invalid");
    await Invoke(function, "../../payroll");
    Check(repository.Reads == before && runtime.Evidence[^1].Result.Code == "invalid-input", "invalid arguments do not reach the repository");
    runtime.BeginTurn("cancelled");
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel();
        try { await function.InvokeAsync(new AIFunctionArguments { ["serviceName"] = "payroll" }, cancelled.Token); Check(false, "cancelled tool must throw"); }
        catch (OperationCanceledException) { Check(runtime.Attempts == 0, "cancellation reaches the injected runtime token"); }
    }
    var otherRuntime = new ToolRuntime(new ActorContext("student-002", new HashSet<string>(["vpn"])), 3, audit);
    otherRuntime.BeginTurn("another-caller");
    new ServiceTools(repository, otherRuntime).GetServiceStatus("payroll");
    Check(otherRuntime.Evidence[^1].Result.Code == "not-available", "another caller does not inherit the first caller's scope");

    using (var host = await AgentHost.CreateAsync(context, auditWriter: TextWriter.Null))
    {
        var first = await host.RunTurnAsync("What is the current status of payroll?");
        var follow = await host.RunTurnAsync("Is a recovery time available for that service?");
        Check(first.Status == "completed" && first.ModelRequests >= 2 && first.Evidence.Any(e => e.Result.Ok), "real MAF pipeline performs a synthetic model/tool/model exchange");
        Check(follow.Evidence.Any(e => e.Result.Data is ServiceStatus { Service: "payroll" }) && first.ConversationId == follow.ConversationId,
            "session history resolves the follow-up subject");
        await host.ResetAsync();
        var fresh = await host.RunTurnAsync("What about that service?");
        Check(fresh.ToolAttempts == 0 && fresh.ConversationId != first.ConversationId, "fresh session excludes the previous conversation");
        var refusal = await host.RunTurnAsync("What is the current status of CRM?");
        Check(refusal.Status == "completed" && refusal.Evidence.All(e => !e.Result.Ok), "completed invocation can contain an unsuccessful business result");
        var restart = await host.RunTurnAsync("Restart payroll now.");
        Check(host.Functions.OfType<AIFunction>().All(f => f.Name == "GetServiceStatus") && restart.ToolAttempts == 0,
            "no write capability is registered");
        var oversize = await host.RunTurnAsync(new string('x', context.Limits.MaxInputCharacters + 1));
        Check(oversize.Status == "rejected" && oversize.ModelRequests == 0, "oversized input is rejected before inference");
        await host.ResetAsync();
        for (int i = 0; i < context.Limits.MaxTurns; i++) await host.RunTurnAsync("What is the current status of VPN?");
        Check((await host.RunTurnAsync("One more turn")).Status == "turn-limit", "conversation turn limit is enforced by host code");
    }
    foreach (var fault in new[] { SyntheticFault.BeforeTool, SyntheticFault.AfterTool, SyntheticFault.Timeout, SyntheticFault.EmptyResponse })
    {
        using var host = await AgentHost.CreateAsync(context, fault: fault, auditWriter: TextWriter.Null);
        string id = host.ConversationId;
        var report = await host.RunTurnAsync("What is the current status of payroll?",
            fault == SyntheticFault.Timeout ? TimeSpan.FromMilliseconds(150) : TimeSpan.FromSeconds(5));
        Check(report.Status == (fault == SyntheticFault.Timeout ? "timed-out" : "failed") && host.ConversationId != id,
            $"{fault}: interruption discards the affected session");
        Check(report.ToolAttempts == (fault == SyntheticFault.AfterTool ? 1 : 0), $"{fault}: evidence identifies whether a tool ran");
    }
    using (var host = await AgentHost.CreateAsync(context, fault: SyntheticFault.RepeatedToolCalls, auditWriter: TextWriter.Null))
    {
        var report = await host.RunTurnAsync("What is the current status of payroll?");
        Check(report.ModelRequests <= context.Limits.MaxIterations + 1, "function rounds are bounded, including a possible final model request");
        Check(report.Status == "completed", "scripted model honors the final request after tool removal");
        Check(report.Evidence.Where(e => e.Attempt > context.Limits.MaxToolAttempts).All(e => !e.Result.Ok), "tool access stops at the independent attempt budget");
    }

    var contacts = new ContactRepository();
    runtime.BeginTurn("combined-budget");
    var contactTool = AIFunctionFactory.Create(new ContactTools(contacts, runtime).GetSupportContact);
    await Invoke(function, "payroll"); await Invoke(contactTool, "payroll"); await Invoke(function, "vpn");
    int contactReads = contacts.Reads;
    await Invoke(contactTool, "vpn");
    Check(contacts.Reads == contactReads && runtime.Evidence[^1].Result.Code == "tool-budget-exceeded", "status and contact tools consume one shared budget");
    runtime.BeginTurn("denied-contact");
    await Invoke(contactTool, "crm");
    Check(contacts.Reads == contactReads && runtime.Evidence[^1].Result.Data is null, "contact tool enforces caller scope before lookup");
    using (var host = await AgentHost.CreateAsync(context, extraTools: r => [AIFunctionFactory.Create(new ContactTools(contacts, r).GetSupportContact)],
        additionalInstructions: "Use GetSupportContact for contact requests.", auditWriter: TextWriter.Null))
    {
        await host.RunTurnAsync("What is the current payroll status?");
        var report = await host.RunTurnAsync("Who should I contact about that service?");
        Check(report.Evidence.Any(e => e.Tool == "GetSupportContact" && e.Result.Ok), "contact follow-up selects the registered second tool");
    }

    var tickets = new TicketRepository(); var ticketContext = new TicketRuntime("student-001");
    var approvals = new ApprovalAuthority(); var ticketTools = new TicketTools(tickets, ticketContext, approvals);
    ticketTools.ReadTicket("TCK-2001");
    Check(ticketTools.LastResult is { Code: "not-available", Data: null }, "ticket history references do not grant ownership");
    ticketTools.CloseTicket("TCK-1001");
    Check(ticketTools.LastResult!.Code == "approval-required" && tickets.FindOwned("TCK-1001", "student-001")!.Status == "open", "ticket close requires approval before mutation");
    string approval = approvals.Issue("reviewer-001", "student-001", "TCK-1001");
    ticketContext.AttachApproval(approval);
    ticketTools.CloseTicket("TCK-1002");
    Check(ticketTools.LastResult!.Code == "approval-required", "approval is bound to the exact ticket and operation");
    ticketTools.CloseTicket("TCK-1001");
    Check(ticketTools.LastResult is { Ok: true, Code: "closed" } && tickets.FindOwned("TCK-1001", "student-001")!.Status == "closed", "close success comes from the business record");
    Check(!approvals.Consume(approval, "student-001", "TCK-1001"), "consumed approval cannot be reused");
    ticketTools.CloseTicket("TCK-1001");
    Check(ticketTools.LastResult!.Code == "already-closed", "repeated close returns the confirmed completed state");
    try { approvals.Issue("claimed-manager", "student-001", "TCK-1001"); Check(false, "untrusted reviewer must be rejected"); }
    catch (UnauthorizedAccessException) { Check(true, "a claimed reviewer cannot issue approval"); }

    string prefix = "UNIT03_CHECK_" + Guid.NewGuid().ToString("N");
    string file = Path.GetTempFileName();
    try
    {
        Environment.SetEnvironmentVariable(prefix + "_A", "process-value");
        File.WriteAllText(file, $"# Synthetic parser fixture\n{prefix}_A=file-value\n{prefix}_B=\"value with spaces\"\n");
        DotEnv.Load(file);
        Check(Environment.GetEnvironmentVariable(prefix + "_A") == "process-value" && Environment.GetEnvironmentVariable(prefix + "_B") == "value with spaces",
            "root dotenv parser preserves process overrides and quoted values");
    }
    finally { File.Delete(file); Environment.SetEnvironmentVariable(prefix + "_A", null); Environment.SetEnvironmentVariable(prefix + "_B", null); }
    context.Save("verification", new { status = "passed", count = checks.Count, checks, azureRequests = 0 });
    Console.WriteLine($"PASS: {checks.Count} offline checks. No Azure requests.");
});

void Check(bool success, string name)
{
    if (!success) { Console.Error.WriteLine($"FAIL: {name}"); throw new InvalidOperationException(name); }
    checks.Add(name); Console.WriteLine($"PASS: {name}");
}
static async Task Invoke(AIFunction function, string service) =>
    await function.InvokeAsync(new AIFunctionArguments { ["serviceName"] = service });
