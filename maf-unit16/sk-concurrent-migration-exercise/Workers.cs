#pragma warning disable SKEXP0080

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.SemanticKernel;

// Dummy data and delays only: no business API, LLM or external write is used.
public sealed record SupportRequest(string CaseId, string Service, string Outcome, int StatusMs, int PolicyMs);
public sealed record StatusEvidence(SupportRequest Request, string Status);
public sealed record PolicyEvidence(SupportRequest Request, bool ReviewAllowed);
public sealed record ValidatedEvidence(SupportRequest Request, string Status, bool ReviewAllowed);

public sealed class ReadStatusStep : KernelProcessStep
{
    [KernelFunction]
    public async Task<StatusEvidence> ReadAsync(SupportRequest request)
    {
        TraceLog.Begin("status");
        await Task.Delay(request.StatusMs);
        TraceLog.End("status");
        return new(request, "degraded");
    }
}

public sealed class ReadPolicyStep : KernelProcessStep
{
    [KernelFunction]
    public async Task<PolicyEvidence> ReadAsync(SupportRequest request)
    {
        TraceLog.Begin("policy");
        await Task.Delay(request.PolicyMs);
        TraceLog.End("policy");
        return new(request, true);
    }
}

public sealed class ValidateEvidenceStep : KernelProcessStep
{
    [KernelFunction]
    public async Task ValidateAsync(StatusEvidence status, PolicyEvidence policy, KernelProcessStepContext context)
    {
        Interlocked.Increment(ref TraceLog.JoinCount);
        if (status.Request.CaseId != policy.Request.CaseId || status.Request.Service != policy.Request.Service)
            throw new InvalidOperationException("Evidence belongs to different requests.");
        if (!TraceLog.Finished("status") || !TraceLog.Finished("policy"))
            throw new InvalidOperationException("Join started before both branches completed.");
        TraceLog.Write("JOIN", "Both required inputs received. ValidateEvidenceStep executes once.");
        TraceLog.Write("STATE", "EvidenceValidated");
        var evidence = new ValidatedEvidence(status.Request, status.Status, policy.ReviewAllowed);
        TraceLog.Write("SK EVENT", "EmitEventAsync(RequestReview)");
        await context.EmitEventAsync("RequestReview", evidence);
    }
}

public sealed class ReviewStep : KernelProcessStep
{
    [KernelFunction]
    public async Task ReviewAsync(ValidatedEvidence evidence, KernelProcessStepContext context)
    {
        TraceLog.Write("STATE", "ReviewPending");
        await Task.Delay(100);
        string eventId = evidence.Request.Outcome switch
        {
            "approved" => "TrustedReview",
            "expired" => "ApprovalExpired",
            "budget" => "TurnLimitReached",
            _ => throw new InvalidOperationException("Unsupported dummy outcome.")
        };
        TraceLog.Write("REVIEW STUB", $"Synthetic outcome={evidence.Request.Outcome}. No real approval is collected.");
        TraceLog.Write("SK EVENT", $"EmitEventAsync({eventId})");
        await context.EmitEventAsync(eventId, evidence);
    }
}

public sealed class FinishStep : KernelProcessStep
{
    [KernelFunction("Completed")]
    public Task CompleteAsync(ValidatedEvidence evidence, KernelProcessStepContext context)
        => FinishAsync("Completed", evidence, context);

    [KernelFunction("Expired")]
    public Task ExpireAsync(ValidatedEvidence evidence, KernelProcessStepContext context)
        => FinishAsync("Expired", evidence, context);

    [KernelFunction("BudgetExhausted")]
    public Task ExhaustAsync(ValidatedEvidence evidence, KernelProcessStepContext context)
        => FinishAsync("BudgetExhausted", evidence, context);

    private static async Task FinishAsync(string state, ValidatedEvidence evidence, KernelProcessStepContext context)
    {
        Interlocked.Increment(ref TraceLog.TerminalCount);
        TraceLog.TerminalState = state;
        TraceLog.Write("STATE", $"{state} | case={evidence.Request.CaseId} | service={evidence.Request.Service}");
        await context.EmitEventAsync("Finished", state);
    }
}

// Console instrumentation for this single-request exercise; not persisted business state.
public static class TraceLog
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly ConcurrentDictionary<string, long> Starts = new();
    private static readonly ConcurrentDictionary<string, long> Ends = new();
    public static int JoinCount;
    public static int TerminalCount;
    public static string? TerminalState;

    public static void Write(string kind, string message)
        => Console.WriteLine($"[{Clock.ElapsedMilliseconds,5} ms] [{kind}] {message}");
    public static void Begin(string branch)
    {
        Starts[branch] = Clock.ElapsedTicks;
        Write("BEGIN", branch);
    }
    public static void End(string branch)
    {
        Ends[branch] = Clock.ElapsedTicks;
        Write("END", branch);
    }
    public static bool Finished(string branch) => Ends.ContainsKey(branch);
    public static void Verify(string outcome)
    {
        bool overlap = Starts["status"] < Ends["policy"] && Starts["policy"] < Ends["status"];
        string expected = outcome switch { "approved" => "Completed", "expired" => "Expired", _ => "BudgetExhausted" };
        Check("Evidence branches overlap in time", overlap);
        Check("Join executed exactly once", JoinCount == 1);
        Check("One expected terminal outcome", TerminalCount == 1 && TerminalState == expected);
    }
    private static void Check(string name, bool passed)
    {
        Write("CHECK", $"{(passed ? "PASS" : "FAIL")} | {name}");
        if (!passed) throw new InvalidOperationException(name);
    }
}
