using System.Diagnostics;

namespace Unit08.Common;

public sealed class Supervisor(IAgentBackend backend, RuntimeSettings settings, AuditTrail audit)
{
    public ExecutionMetrics Metrics { get; } = new();

    public static Delegation[] Plan(SupportCase input)
    {
        if (string.IsNullOrWhiteSpace(input.RunId)
            || input.Scenario is not ("vpn" or "payroll" or "combined"))
            throw new ArgumentException("invalid-case");
        var tasks = new List<Delegation>();
        if (input.Scenario is "vpn" or "combined")
            tasks.Add(new(input.RunId, $"{input.RunId}:network", "network",
                "Assess the VPN connectivity evidence and suggest a diagnostic step.",
                new("catalog/vpn/v1", "vpn", "healthy",
                    "No general VPN outage. Collect error code and client version.")));
        if (input.Scenario is "payroll" or "combined")
            tasks.Add(new(input.RunId, $"{input.RunId}:service", "service",
                "Assess the payroll service evidence and suggest the next support step.",
                new("catalog/payroll/v1", "payroll", "degraded",
                    "Service is degraded. No recovery time is available.")));
        return tasks.ToArray();
    }

    public async Task<CaseOutcome> RunAsync(SupportCase input, RunOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Delegation[] tasks = Plan(input);
        if (options.FailNetwork && tasks.All(t => t.Role != "network"))
            throw new ArgumentException("--fail-network requires vpn or combined.");
        audit.Emit(input.RunId, "plan", "coordinator", new { Tasks = tasks.Length });
        // The limiter and reports belong to this run, not to all host requests.
        using var slots = new SemaphoreSlim(settings.Concurrency);
        async Task<Report> Execute(Delegation task)
        {
            await slots.WaitAsync(ct);
            try { return await ConsultAsync(task, options, ct); }
            finally { slots.Release(); }
        }
        Report[] reports;
        if (options.Serial)
        {
            var list = new List<Report>();
            foreach (var task in tasks) list.Add(await Execute(task));
            reports = list.ToArray();
        }
        else reports = await Task.WhenAll(tasks.Select(Execute));
        if (reports.Any(r => r.Outcome != "accepted"))
        {
            audit.Emit(input.RunId, "review-required", "coordinator", new
                { Failed = reports.Count(r => r.Outcome != "accepted") });
            return new(input.RunId, "needs-review", reports, null);
        }
        return await CompleteAsync(input, reports, ct);
    }

    public async Task<Report> ConsultAsync(Delegation task, RunOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string subject = task.Role switch
        {
            "network" => "vpn", "service" => "payroll",
            _ => throw new ArgumentException("unknown-role")
        };
        if (string.IsNullOrWhiteSpace(task.RunId) || task.TaskId != $"{task.RunId}:{task.Role}"
            || task.Evidence.Subject != subject)
            throw new ArgumentException("invalid-assignment");
        var watch = Stopwatch.StartNew();
        Metrics.EnterSpecialist();
        audit.Emit(task.RunId, "specialist-start", task.Role, new { task.TaskId });
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(settings.SpecialistTimeoutSeconds));
            if (options.FailNetwork && task.Role == "network")
                throw new TimeoutException("simulated-network-timeout");
            Advice advice = await backend.AnalyzeAsync(task, deadline.Token);
            if (options.Conflict) advice = advice with { ObservedStatus = "recovered" };
            Contract.Validate(task, advice);
            audit.Emit(task.RunId, "specialist-accepted", task.Role, new { task.TaskId });
            return new(task, "accepted", advice, "", watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            string code = ex switch
            {
                TimeoutException or OperationCanceledException => "specialist-timeout",
                InvalidOperationException when ex.Message == "unsupported-observation"
                    => "unsupported-observation",
                InvalidOperationException when ex.Message == "unsupported-action"
                    => "unsupported-action",
                _ => "specialist-failed"
            };
            audit.Emit(task.RunId, "specialist-rejected", task.Role,
                new { task.TaskId, Code = code, ErrorType = ex.GetType().Name });
            return new(task, "rejected", null, code, watch.ElapsedMilliseconds);
        }
        finally { Metrics.LeaveSpecialist(); }
    }

    public async Task<CaseOutcome> CompleteAsync(SupportCase input, Report[] reports, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Delegation[] plan = Plan(input);
        if (reports.Length != plan.Length
            || reports.Select(r => r.Task.TaskId).Distinct().Count() != plan.Length)
            throw new InvalidOperationException("incomplete-plan");
        foreach (var task in plan)
        {
            var report = reports.SingleOrDefault(r => r.Task.TaskId == task.TaskId)
                ?? throw new InvalidOperationException("incomplete-plan");
            Contract.ValidateReport(task, report);
        }
        string stage = reports.Any(r => r.Task.Evidence.Subject == "payroll"
            && r.Task.Evidence.Status == "degraded") ? "escalation-proposal" : "information-ready";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(settings.CoordinatorTimeoutSeconds));
        Metrics.EnterSynthesis();
        audit.Emit(input.RunId, "synthesis-start", "coordinator", new { Stage = stage });
        Brief brief;
        try
        {
            brief = await backend.SummarizeAsync(stage, reports, deadline.Token);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[DEBUG] SummarizeAsync failed: " + ex);
            throw;
        }
        Console.Error.WriteLine("[DEBUG] Brief: " + System.Text.Json.JsonSerializer.Serialize(brief));
        Console.Error.WriteLine("[DEBUG] Expected evidence ids: "
            + string.Join(",", reports.Select(r => r.Task.Evidence.SourceId)));
        try
        {
            Contract.ValidateBrief(reports, brief);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[DEBUG] ValidateBrief failed: " + ex.Message);
            throw;
        }
        audit.Emit(input.RunId, "completed", "coordinator", new { Stage = stage });
        return new(input.RunId, stage, reports, brief);
    }
}
