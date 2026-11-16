using System.Text.Json;
using Unit08.Common;
using Unit08.Exercises;

int checks = 0;
void Require(bool passed, string name)
{
    if (!passed) throw new Exception(name);
    checks++;
}
void Reject(Action action, string code)
{
    try { action(); }
    catch (InvalidOperationException ex) when (ex.Message == code) { checks++; return; }
    throw new Exception("Expected " + code);
}
Report[] ReportsFor(SupportCase input) => Supervisor.Plan(input)
    .Select(t => new Report(t, "accepted", DemoBackend.AdviceFor(t), "", 0)).ToArray();
CaseOutcome OutcomeFor(SupportCase input, Report[] reports) => new(input.RunId,
    input.Scenario == "vpn" ? "information-ready" : "escalation-proposal", reports,
    new Brief("Evidence supports the proposed diagnostic and referral steps.",
        reports.Select(r => r.Task.Evidence.SourceId).ToArray()));

var settings = new RuntimeSettings(false, NetworkDelayMs: 20, ServiceDelayMs: 30);
var audit = new AuditTrail(false);
var supervisor = new Supervisor(new DemoBackend(settings), settings, audit);
var firstCase = new SupportCase("run-a", "combined");
var secondCase = new SupportCase("run-b", "combined");
var outcomes = await Task.WhenAll(supervisor.RunAsync(firstCase, new(), CancellationToken.None),
    supervisor.RunAsync(secondCase, new(), CancellationToken.None));
Require(outcomes.All(o => o.Stage == "escalation-proposal"), "Successful combined runs");
Require(outcomes.All(o => o.Reports.All(r => r.Task.RunId == o.RunId
    && r.Task.TaskId.StartsWith(o.RunId + ":"))), "Concurrent case isolation");
foreach (var outcome in outcomes)
{
    var events = audit.Events.Where(e => e.RunId == outcome.RunId).ToArray();
    Require(Array.FindIndex(events, e => e.Event == "synthesis-start")
        > Array.FindLastIndex(events, e => e.Event == "specialist-accepted"), "Collection before synthesis");
}

Report[] reports = ReportsFor(firstCase);
var task = reports[0].Task;
Advice valid = reports[0].Advice!;
Reject(() => Contract.Validate(task, valid with { EvidenceId = "invented-source" }), "unsupported-observation");
Reject(() => Contract.Validate(task, valid with { ObservedStatus = "recovered" }), "unsupported-observation");
Reject(() => Contract.Validate(task, valid with { Action = "reset-credentials" }), "unsupported-action");
Reject(() => Contract.Validate(task, valid with { Reason = new string('x', 601) }), "invalid-reason");
Reject(() => Contract.ValidateReport(task, reports[0] with
    { Task = task with { RunId = "another-run" } }), "assignment-mismatch");
Reject(() => Contract.ValidateBrief(reports, new("Brief", ["catalog/vpn/v1", "catalog/vpn/v1"])), "invalid-brief");
Reject(() => Contract.ValidateBrief(reports, new("", ["catalog/vpn/v1", "catalog/payroll/v1"])), "invalid-brief");

var partial = await supervisor.RunAsync(new("partial", "combined"), new(FailNetwork: true), CancellationToken.None);
Require(partial.Stage == "needs-review" && partial.Brief is null
    && partial.Reports.Single(r => r.Task.Role == "service").Outcome == "accepted", "Partial failure preserved");
Require(audit.Events.Where(e => e.RunId == "partial").All(e => e.Event != "synthesis-start"), "No partial synthesis");
var conflict = await supervisor.RunAsync(new("conflict", "payroll"), new(Conflict: true), CancellationToken.None);
Require(conflict.Reports.Single().ErrorCode == "unsupported-observation" && conflict.Brief is null,
    "Contradiction blocks synthesis");

// Exercise an actual cancellation deadline in addition to the injected timeout.
var timeoutSettings = settings with { SpecialistTimeoutSeconds = 1, NetworkDelayMs = 1200, ServiceDelayMs = 0 };
var timeoutSupervisor = new Supervisor(new DemoBackend(timeoutSettings), timeoutSettings, new(false));
var timedOut = await timeoutSupervisor.RunAsync(new("timeout", "combined"), new(), CancellationToken.None);
Require(timedOut.Stage == "needs-review"
    && timedOut.Reports.Single(r => r.Task.Role == "network").ErrorCode == "specialist-timeout", "Real specialist deadline");
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    try { await supervisor.RunAsync(new("cancelled", "combined"), new(), cancelled.Token); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { checks++; }
    Require(audit.Events.All(e => e.RunId != "cancelled"), "Cancelled case starts no work");
}

var raw = new SyntheticIntake("E809", "degraded", "mfa-required", "EMP-TEST-001", "FAKE-TOKEN-NOT-A-CREDENTIAL");
var deniedReader = new IdentityEvidenceReader();
try { IdentityDelegation.Plan("id-a", raw, false, deniedReader); throw new Exception("Unauthorized read"); }
catch (UnauthorizedAccessException) { checks++; }
Require(deniedReader.Reads == 0, "Authorization precedes retrieval");
var reader = new IdentityEvidenceReader();
var scoped = IdentityDelegation.Plan("id-b", raw, true, reader);
string serialized = JsonSerializer.Serialize(scoped);
Require(scoped.Length == 3 && !serialized.Contains(raw.TestEmployeeId)
    && !serialized.Contains(raw.FakeAccessToken), "Minimal role envelopes");
Require(scoped.Where(t => t.Role != "identity").All(t =>
    !JsonSerializer.Serialize(t).Contains(raw.AuthenticationCategory)), "No cross-role identity data");
var identity = scoped.Single(t => t.Role == "identity");
Reject(() => IdentityDelegation.ValidateIdentityAdvice(identity,
    new(identity.Evidence.SourceId, identity.Evidence.Status, "reset-credentials", "Reset it.")), "unsupported-action");

var reducer = new ReportReducer(firstCase);
Require(reducer.Accept(reports[0]).Status == "accepted", "First delivery accepted");
Require(reducer.Accept(reports[0] with { ElapsedMs = 999 }).Status == "identical-replay", "Delivery metadata is not a new result");
Require(reducer.Accept(reports[0] with { Task = task with { RunId = "foreign" } }).Status == "foreign-run-rejected", "Foreign run rejected");
Require(reducer.Accept(reports[0] with { Task = task with { TaskId = "unknown" } }).Status == "unplanned-task-rejected", "Unknown task rejected");
Require(reducer.Accept(reports[0] with { Task = task with { Evidence = task.Evidence with { Detail = "changed" } } }).Status
    == "assignment-mismatch", "Original task evidence bound");
Reject(() => reducer.CompleteOnceAsync((r, ct) => Task.FromResult(OutcomeFor(firstCase, r)), CancellationToken.None), "incomplete-plan");
await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() => reducer.Accept(reports[i % 2]))));
Require(reducer.AcceptedReports.Length == 2, "Concurrent duplicate deliveries reduced once");
int calls = 0;
Task<CaseOutcome> Synthesize(Report[] accepted, CancellationToken ct)
{
    Interlocked.Increment(ref calls);
    return Task.FromResult(OutcomeFor(firstCase, accepted));
}
await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => reducer.CompleteOnceAsync(Synthesize, CancellationToken.None)));
Require(calls == 1, "One synthesis reservation for concurrent callers");
var changed = reports[0] with { Advice = valid with { Reason = "A different supported explanation." } };
Require(reducer.Accept(changed).Status == "conflicting-replay", "Conflicting replay flagged");
var superseded = await reducer.CompleteOnceAsync(Synthesize, CancellationToken.None);
Require(calls == 1 && superseded.Stage == "needs-review" && superseded.Brief is null
    && reducer.AcceptedReports[0].Advice == valid, "Late conflict supersedes without overwrite or new inference");

// Hold synthesis open to test the conflict-versus-publication race deterministically.
var inFlight = new ReportReducer(secondCase);
Report[] secondReports = ReportsFor(secondCase);
foreach (var report in secondReports) inFlight.Accept(report);
var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
int inFlightCalls = 0;
async Task<CaseOutcome> HeldSynthesis(Report[] accepted, CancellationToken ct)
{
    Interlocked.Increment(ref inFlightCalls);
    await release.Task.WaitAsync(ct);
    return OutcomeFor(secondCase, accepted);
}
Task<CaseOutcome> pending = inFlight.CompleteOnceAsync(HeldSynthesis, CancellationToken.None);
inFlight.Accept(secondReports[0] with { Advice = secondReports[0].Advice! with { Reason = "Conflicting result." } });
release.SetResult(true);
var withheld = await pending;
Require(inFlightCalls == 1 && withheld.Stage == "needs-review" && withheld.Brief is null,
    "Conflict while synthesizing suppresses publication");

Console.WriteLine(JsonSerializer.Serialize(new { Stage = "checks-passed", Checks = checks,
    Coverage = "contracts, deadlines, cancellation, case isolation, scopes, replay and publication races" }));
