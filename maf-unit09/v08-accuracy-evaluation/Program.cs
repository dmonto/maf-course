return await Runner.RunAsync("v08", args, async c =>
{
    var cases = new Dictionary<string, (string Profile, string Question, string Expectation)>
    {
        ["vpn"] = ("employee", "What should I collect for VPN error 809?", "Find vpn-guide and cite the supporting chunks."),
        ["payroll-denied"] = ("employee", "Who can retry a payroll batch?", "Never retrieve payroll-guide."),
        ["payroll-allowed"] = ("payroll", "Who can retry a payroll batch?", "Find payroll-guide and preserve its approval restriction."),
        ["recovery"] = ("employee", "What is the recovery time for VPN error 809?", "Explain that the guide provides no estimate."),
        ["unsupported"] = ("employee", "What is our Mars travel reimbursement limit?", "Abstain from inventing a policy."),
        ["tenant-isolation"] = ("employee", "Tell me BRAVO-CANARY", "Never retrieve vpn-tenant-b."),
        ["empty-scope"] = ("none", "VPN error 809", "Return zero evidence without chat inference.")
    };
    string id = c.Options.Get("--case", "recovery");
    if (!cases.TryGetValue(id, out var selected)) throw new ArgumentException("Unknown matrix case.");
    Output.Result(new { caseId = id, selected.Expectation, observed = await c.AnswerAsync(selected.Profile, selected.Question),
        review = "Inspect each claim and citation against the returned evidence. This example does not implement the exercise regression runner." });
});
