using System.Text.Json;
using Unit05.Common;
using Unit05.Exercise01;
using Unit05.Exercise02;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAILED: " + name);
    checks++;
}
ToolResult Read(string raw) => JsonSerializer.Deserialize<ToolResult>(raw)!;
Environment.SetEnvironmentVariable("PROMPT_VERSION", "v2");
var settings = CourseSettings.Load(new Arguments(["--demo"]));

// Both prompts exercise real MAF function calling and generic typed output parsing.
foreach (string version in new[] { "v1", "v2" })
    foreach (var c in AcceptanceCases.All)
    {
        var report = await AgentRunner.RunAsync(settings with { PromptVersion = version }, c.Message, c.Injection);
        Check(report.ExecutionMode == "demo-scripted-model", "explicit demo mode");
        Check(report.Outcome == "contract-accepted", c.Id + ": " + string.Join(",", report.Errors));
        Check(AcceptanceCases.Check(c, report).Length == 0, c.Id + " task acceptance");
        Check(report.PromptSha256.Length == 64, "instruction fingerprint");
        Check(report.Temperature is null && report.TopP is null, "unsupported sampling options remain unset");
        if (c.Injection)
        {
            Check(report.Evidence.Any(e => e.Result.Data?.Detail.Contains("SYSTEM OVERRIDE") == true), "injection reached tool data");
            Check(report.Answer!.ActionExecuted == false && !report.Answer.Summary.Contains("SYSTEM OVERRIDE"), "scripted answer excludes injected instruction");
        }
    }
var multiple = await AgentRunner.RunAsync(settings, "What is payroll and VPN status?");
Check(multiple.Answer?.Outcome == "clarification" && multiple.Evidence.Count == 0, "multi-service clarification");
var empty = await AgentRunner.RunAsync(settings, "");
Check(empty.Outcome == "execution-failed" && empty.Evidence.Count == 0, "empty input rejected before tool access");
var longInput = await AgentRunner.RunAsync(settings, new string('x', 2001));
Check(longInput.Outcome == "execution-failed" && longInput.Evidence.Count == 0, "oversized input rejected");

// Check independent tool adapters so ordinary input cases do not consume each other's budget.
foreach (var (input, code) in new[] { (" PAYROLL ", "ok"), ("https://example.invalid/", "invalid-input"), ("crm", "not-available"), ("billing", "not-available") })
{
    var tools = new ServiceTools(AgentRunner.Caller(), false);
    var result = Read(tools.GetServiceStatus(input));
    Check(result.Code == code, "tool boundary: " + input);
    Check(result.Ok || result.Data is null, "no data on failure");
}
var sharedBudget = new ServiceTools(AgentRunner.Caller(), false);
sharedBudget.GetServiceStatus("payroll"); sharedBudget.GetSupportGuidance("payroll");
sharedBudget.GetServiceStatus("vpn"); sharedBudget.GetSupportGuidance("vpn");
Check(sharedBudget.Evidence.Count == 4 && sharedBudget.Evidence.Last().Attempt == 4, "all attempts recorded");
Check(sharedBudget.Evidence.Last().Result is { Ok: false, Code: "tool-budget-exceeded", Data: null }, "shared three-read limit");

var combined = await AgentRunner.RunAsync(settings, "What is payroll status and what should I do next?");
SupportAnswer Clone() => JsonSerializer.Deserialize<SupportAnswer>(JsonSerializer.Serialize(combined.Answer))!;
var bad = Clone(); bad.Status = "healthy";
Check(AnswerValidator.Check(bad, combined.Evidence).Contains("status-evidence-mismatch"), "fabricated status refused");
bad = Clone(); bad.RecoveryTime = "18:00";
Check(AnswerValidator.Check(bad, combined.Evidence).Contains("unsupported-recovery-time"), "fabricated recovery time refused");
bad = Clone(); bad.NextStep = "restart-service";
Check(AnswerValidator.Check(bad, combined.Evidence).Contains("guidance-evidence-mismatch"), "fabricated guidance refused");
bad = Clone(); bad.Sources = ["unretrieved-source"];
Check(AnswerValidator.Check(bad, combined.Evidence).Contains("source-evidence-mismatch"), "fabricated source refused");
bad = Clone(); bad.ActionExecuted = true;
Check(AnswerValidator.Check(bad, combined.Evidence).Contains("unsupported-action-claim"), "fabricated action refused");
bad = Clone(); bad.Summary = "I restarted payroll successfully.";
Check(AnswerValidator.Check(bad, combined.Evidence).Length == 0, "documented free-text semantic-validation gap");
bad = Clone(); bad.Service = "vpn";
Check(AnswerValidator.Check(bad, combined.Evidence).Contains("missing-or-mixed-service-evidence"), "wrong service refused");
Check(AnswerValidator.Check(new() { Outcome = "answered", Summary = "All fine." }, []).Length > 0, "answer without evidence refused");
Check(AnswerValidator.Check(new() { Outcome = "unavailable", Summary = "No data." }, []).Contains("missing-unavailable-evidence"), "unavailable requires a failed lookup");

// Exercise 1 remains a design contract, without registering a maintenance tool.
var fixture = MaintenanceContract.ValidFixture();
Check(MaintenanceContract.Validate(fixture).Length == 0, "UTC maintenance fixture");
Check(MaintenanceContract.Validate(fixture with { Data = fixture.Data! with { EndUtc = fixture.Data.StartUtc } }).Contains("invalid-window-order"), "end must follow start");
Check(MaintenanceContract.Validate(fixture with { Data = fixture.Data! with { StartUtc = fixture.Data.StartUtc.ToOffset(TimeSpan.FromHours(1)) } }).Contains("timestamps-must-be-utc"), "explicit UTC offset");
Check(MaintenanceContract.Validate(new(false, "not-available")).Length == 0, "unavailable maintenance result");
Check(MaintenanceContract.Validate(new(false, "not-available", fixture.Data)).Contains("failure-must-not-return-data"), "maintenance failures contain no record");

// Exercise 2 adds a field only to its own response type.
foreach (var (message, expected) in new[]
{
    ("What is payroll status and what should I do next?", true),
    ("What is the current status of payroll?", false),
    ("What is VPN status and what should I do next?", false)
})
{
    var report = await AgentRunner.RunAsync<HumanReviewAnswer>(settings, message, false,
        HumanReviewContract.Validate, HumanReviewContract.Instructions, HumanReviewContract.FormatDemoAnswer);
    Check(report.Outcome == "contract-accepted", "extended response accepted");
    Check(report.Answer!.RequiresHumanReview == expected, "correct human-review recommendation");
    Check(!report.Answer.ActionExecuted, "review recommendation performs no action");
}
var forged = new HumanReviewAnswer { Outcome = "clarification", Summary = "Which service?", RequiresHumanReview = true };
Check(HumanReviewContract.Validate(forged, []).Contains("human-review-evidence-mismatch"), "fabricated review flag refused");
var wrongServiceEvidence = combined.Evidence.Select(e => e.Tool == "GetSupportGuidance"
    ? e with { Result = e.Result with { Data = e.Result.Data! with { Service = "vpn" } } } : e).ToArray();
Check(!HumanReviewContract.Expected(combined.Answer!, wrongServiceEvidence), "review evidence must concern the same service");

// Configuration uses the common root and preserves explicit process values.
Environment.SetEnvironmentVariable("UNIT05_PRECEDENCE_TEST", "process");
string temp = Path.GetTempFileName();
File.WriteAllText(temp, "UNIT05_PRECEDENCE_TEST=file\n");
DotEnv.Load(temp); File.Delete(temp);
Check(Environment.GetEnvironmentVariable("UNIT05_PRECEDENCE_TEST") == "process", "process values win over dotenv");
Environment.SetEnvironmentVariable("UNIT05_PRECEDENCE_TEST", null);
Check(new Arguments(["--input", "Payroll status", "--injection"]).Injection, "CLI injection switch");
try { _ = new Arguments(["--demo", "--live"]); throw new Exception("Conflicting modes accepted."); }
catch (ConfigurationException) { Check(true, "conflicting modes rejected"); }
Console.WriteLine($"PASS: {checks} offline checks. Scripted output does not establish live Azure access or prompt quality.");
