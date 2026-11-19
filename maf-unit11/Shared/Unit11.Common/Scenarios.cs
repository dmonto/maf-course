namespace Maf.Unit11;

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

public static class Scenarios
{
    public static Task QueueAsync(ExampleContext ctx)
    {
        ctx.Args.Allow(); ctx.RequireDemo();
        var store = ctx.Store(); SupportEvent e = ctx.Event("QUEUE");
        bool created = store.Accept(e), duplicate = store.Accept(e);
        bool conflictRejected = false;
        try { store.Accept(e with { Data = e.Data with { Status = "healthy" } }); }
        catch (InvalidDataException) { conflictRejected = true; }
        Claim claim = store.TryClaim() ?? throw new InvalidOperationException("No work was claimed.");
        ConsoleResult.Check(claim.Event.Id == e.Id, "Unexpected pending work; use a separate database for this run.");
        bool committed = store.Complete(claim, "Local queue mechanics completed; no model was invoked.", null, 0);
        ConsoleResult.Check(created && !duplicate && conflictRejected && committed, "Queue contract failed.");
        var job = store.Read(e.Id).Single();

        // Readable summary; the RESULT JSON line printed afterwards stays last.
        Console.WriteLine($"Example: {ctx.Example}   Execution: local-queue   Model invoked: no");
        Console.WriteLine($"Event: {e.Id}");
        Console.WriteLine($"Database: {ctx.Database}");
        Console.WriteLine("\nQueue contract:");
        Console.WriteLine($"  PASS  New event accepted (created: {created})");
        Console.WriteLine($"  PASS  Same event id and payload is a duplicate, not a second job (duplicate created: {duplicate})");
        Console.WriteLine($"  PASS  Same event id with a different payload is rejected (conflict rejected: {conflictRejected})");
        Console.WriteLine($"  PASS  Claimed work completed and committed");
        Console.WriteLine($"\nJob: state {job.State}, {job.Attempts} attempt(s), processing {job.ProcessingMs} ms");
        Console.WriteLine($"  Result: {job.Result}\n");

        ConsoleResult.Write(new { status = "passed", example = ctx.Example, execution = "local-queue",
            eventId = e.Id, created, duplicateCreated = duplicate, conflictRejected,
            job, database = ctx.Database });
        return Task.CompletedTask;
    }

    public static async Task RetryAsync(ExampleContext ctx)
    {
        ctx.Args.Allow("delay-ms"); ctx.RequireDemo();
        var store = ctx.Store(); SupportEvent retry = ctx.Event("RETRY"), poison = ctx.Event("POISON");
        store.Accept(retry); store.Accept(poison);
        var jobs = await LabHosting.ProcessAsync(ctx, store,
            new FixtureReasoner(ctx.FragmentDelayMs, failFirst: true, poisonId: poison.Id), retry.Id, poison.Id);
        ConsoleResult.Check(jobs[0].State == "Completed" && jobs[0].Attempts == 2 &&
            jobs[0].Result!.Split("Reported service").Length == 2 &&
            jobs[1].State == "Dead" && jobs[1].Attempts == 1, "Retry or quarantine contract failed.");

        // Readable summary; the RESULT JSON line printed afterwards stays last.
        Console.WriteLine($"\nExample: {ctx.Example}   Execution: local-fixture   Model invoked: no");
        Console.WriteLine($"Database: {ctx.Database}");
        Console.WriteLine("\nJobs:");
        foreach (var j in jobs)
        {
            Console.WriteLine($"  {j.Id}");
            Console.WriteLine($"    State: {j.State}   Attempts: {j.Attempts}" +
                (j.ProcessingMs is null ? "" : $"   Processing: {j.ProcessingMs:F0} ms") +
                (j.FirstTextMs is null ? "" : $"   First text: {j.FirstTextMs:F0} ms"));
            if (j.Result is not null) Console.WriteLine($"    Result: {j.Result}");
            if (j.ErrorCode is not null) Console.WriteLine($"    Error: {j.ErrorCode}");
        }
        Console.WriteLine("\nChecks:");
        Console.WriteLine("  PASS  Transient failure was retried and completed on attempt 2");
        Console.WriteLine("  PASS  Poison event was quarantined as Dead after 1 attempt (not retried)");
        Console.WriteLine("  PASS  Text from the failed first attempt was not committed\n");

        ConsoleResult.Write(new { status = "passed", example = ctx.Example, execution = "local-fixture",
            jobs, partialTextFromFailedAttemptCommitted = false, database = ctx.Database });
    }

    public static async Task ConcurrencyAsync(ExampleContext ctx)
    {
        ctx.Args.Allow("delay-ms", "count"); ctx.RequireDemo();
        int count = ctx.Args.Number("count", 8);
        if (count < 2 || count > 50) throw new ArgumentException("--count must be between 2 and 50.");
        var store = ctx.Store();
        var events = Enumerable.Range(0, count).Select(i =>
            ctx.Event("PARALLEL", i % 2 == 0 ? "vpn" : "erp", i % 2 == 0 ? "degraded" : "unavailable")).ToArray();
        await Task.WhenAll(events.Select(e => Task.Run(() => store.Accept(e))));
        var tracked = new TrackingReasoner(new FixtureReasoner(ctx.FragmentDelayMs));
        var jobs = await LabHosting.ProcessAsync(ctx, store, tracked, events.Select(e => e.Id).ToArray());
        ConsoleResult.Check(tracked.Maximum <= ctx.Workers && jobs.All(j => j.State == "Completed" && j.Attempts == 1),
            "The concurrency bound or result isolation failed.");
        for (int i = 0; i < jobs.Length; i++)
            ConsoleResult.Check(jobs[i].Result!.Contains($"Reported service: {events[i].Data.Service}."), "Wrong event result.");

        // Readable summary; the RESULT JSON line printed afterwards stays last.
        Console.WriteLine($"\nExample: {ctx.Example}   Execution: local-fixture   Model invoked: no");
        Console.WriteLine($"Events: {count}   Configured workers: {ctx.Workers}   Maximum concurrent attempts observed: {tracked.Maximum}");
        Console.WriteLine($"Database: {ctx.Database}");
        Console.WriteLine($"\n  {"#",-3}{"event",-14}{"service",-9}{"state",-11}{"attempts",-10}{"processing",-12}result matches event");
        for (int i = 0; i < jobs.Length; i++)
            Console.WriteLine($"  {i + 1,-3}{jobs[i].Id[^8..],-14}{events[i].Data.Service,-9}{jobs[i].State,-11}" +
                $"{jobs[i].Attempts,-10}{(jobs[i].ProcessingMs is null ? "n/a" : $"{jobs[i].ProcessingMs:F0} ms"),-12}yes");
        Console.WriteLine("\nChecks:");
        Console.WriteLine($"  PASS  Concurrency never exceeded the worker bound ({tracked.Maximum} <= {ctx.Workers})");
        Console.WriteLine("  PASS  Every job completed in 1 attempt");
        Console.WriteLine("  PASS  Each job's result names its own event's service (no cross-contamination)\n");

        ConsoleResult.Write(new { status = "passed", example = ctx.Example, execution = "local-fixture",
            configuredWorkers = ctx.Workers, maximumConcurrentAttempts = tracked.Maximum,
            count, jobs, database = ctx.Database });
    }

    public static async Task PersistenceAsync(ExampleContext ctx)
    {
        ctx.Args.Allow("phase", "event-file", "delay-ms"); ctx.RequireDemo();
        string phase = ctx.Args.Get("phase", "all");
        if (phase == "all")
        {
            SupportEvent e = ctx.Event("RESTART");
            string file = Path.Combine(ctx.DataDirectory, e.Id + ".json");
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(e, LabJson.Options));
            int[] processIds = new int[3]; int index = 0;
            foreach (string childPhase in new[] { "seed", "process", "verify" })
                processIds[index++] = await ChildAsync(childPhase, ctx.Database, file);
            ConsoleResult.Check(processIds.Distinct().Count() == 3, "Independent processes were required.");
            var finalJob = ctx.Store().Read(e.Id).Single();

            // Readable summary; the RESULT JSON line printed afterwards stays last.
            Console.WriteLine($"\nExample: {ctx.Example}   Execution: local-fixture   Model invoked: no");
            Console.WriteLine($"Event: {e.Id}");
            Console.WriteLine($"Envelope: {file}");
            Console.WriteLine($"Database: {ctx.Database}");
            Console.WriteLine("\nPhases (each ran in its own process):");
            string[] phases = ["seed    (accept the event, state Pending)", "process (worker completes the job)",
                "verify  (re-deliver the event; completed work must not change)"];
            for (int i = 0; i < phases.Length; i++) Console.WriteLine($"  process {processIds[i],-7} {phases[i]}");
            Console.WriteLine($"\nFinal job: state {finalJob.State}, {finalJob.Attempts} attempt(s), processing {finalJob.ProcessingMs:F0} ms");
            Console.WriteLine($"  Result: {finalJob.Result}");
            Console.WriteLine("\nChecks:");
            Console.WriteLine("  PASS  Three independent processes were used");
            Console.WriteLine("  PASS  The completed job survived a process restart");
            Console.WriteLine("  PASS  Duplicate delivery did not change the completed result or attempt count\n");

            ConsoleResult.Write(new { status = "passed", example = ctx.Example, execution = "local-fixture",
                independentProcesses = processIds, eventId = e.Id, envelope = file,
                database = ctx.Database, job = finalJob });
            return;
        }
        string eventFile = ctx.Args.Get("event-file", "");
        SupportEvent input = JsonSerializer.Deserialize<SupportEvent>(await File.ReadAllTextAsync(eventFile), LabJson.Options)
            ?? throw new InvalidDataException("Missing saved envelope.");
        var store = ctx.Store();
        if (phase == "seed") ConsoleResult.Check(store.Accept(input), "The seed must be a new event.");
        else if (phase == "process")
        {
            var jobs = await LabHosting.ProcessAsync(ctx, store, new FixtureReasoner(ctx.FragmentDelayMs), input.Id);
            ConsoleResult.Check(jobs.Single().State == "Completed", "The worker did not complete.");
        }
        else if (phase == "verify")
        {
            var before = store.Read(input.Id).Single();
            ConsoleResult.Check(before.State == "Completed" && !store.Accept(input), "Restart lost the completed identity.");
            var after = store.Read(input.Id).Single();
            ConsoleResult.Check(before.Result == after.Result && before.Attempts == after.Attempts,
                "Duplicate delivery changed completed work.");
        }
        else throw new ArgumentException("Use --phase all, seed, process or verify.");
        var phaseJob = store.Read(input.Id).Single();
        Console.WriteLine($"Phase {phase}: process {Environment.ProcessId}, job {phaseJob.State}, {phaseJob.Attempts} attempt(s)");
        ConsoleResult.Write(new { status = "passed", phase, processId = Environment.ProcessId,
            job = phaseJob });
    }

    private static async Task<int> ChildAsync(string phase, string database, string file)
    {
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the current executable.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = EnvironmentConfig.Root
        };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        foreach (string arg in new[] { "--demo", "--phase", phase, "--database", database, "--event-file", file })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start the child process.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        string text = await output, errors = await error;
        Console.Write(text);
        if (process.ExitCode != 0) throw new InvalidOperationException($"Child phase {phase} failed: {errors} {text}");
        return process.Id;
    }

    public static async Task ExternalAsync(ExampleContext ctx)
    {
        ctx.Args.Allow("command", "port", "file", "delay-ms");
        switch (ctx.Args.Get("command", "serve"))
        {
            case "serve": await LabHosting.ServeAsync(ctx); break;
            case "submit": await LabHosting.SubmitAsync(ctx); break;
            case "status": ConsoleResult.Write(ctx.Store().Read()); break;
            default: throw new ArgumentException("Use --command serve, submit or status.");
        }
    }

    public static async Task EnterpriseAsync(ExampleContext ctx)
    {
        ctx.Args.Allow("command", "port", "file", "delay-ms");
        string command = ctx.Args.Get("command", "stream");
        if (command == "serve") { await LabHosting.ServeAsync(ctx, port: 5117); return; }
        if (command == "submit") { await LabHosting.SubmitAsync(ctx, defaultPort: 5117); return; }
        if (command == "status")
        {
            var all = ctx.Store().Read();
            Console.WriteLine($"Jobs in {ctx.Database}: {all.Count()}");
            foreach (var j in all)
                Console.WriteLine($"  {j.Id}  {j.State,-10} {j.Attempts} attempt(s)" + (j.ErrorCode is null ? "" : $"  error {j.ErrorCode}"));
            Console.WriteLine();
            ConsoleResult.Write(all);
            return;
        }
        if (command is "bus" or "bus-send")
        {
            using var stop = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stop.Cancel(); };
            Console.CancelKeyPress += cancel;
            try
            {
                if (command == "bus-send") await ServiceBusBridge.SendAsync(ctx.Args.Get("file", ""), stop.Token);
                else await ServiceBusBridge.ReceiveAsync(ctx.Store(), stop.Token);
            }
            finally { Console.CancelKeyPress -= cancel; }
            return;
        }
        if (command != "stream") throw new ArgumentException("Use stream, serve, submit, status, bus or bus-send.");
        var store = ctx.Store(); SupportEvent input = ctx.Event("ENTERPRISE"); store.Accept(input);
        JobView job = (await LabHosting.ProcessAsync(ctx, store, ctx.Reasoner(), input.Id)).Single();
        ConsoleResult.Check(job.State == "Completed", $"The event ended in {job.State}: {job.ErrorCode}.");

        // Readable summary; the RESULT JSON line printed afterwards stays last.
        Console.WriteLine($"\nExample: {ctx.Example}   Execution: {(ctx.Live ? "azure-maf" : "local-fixture")}   Model invoked: {(ctx.Live ? "yes" : "no")}");
        Console.WriteLine($"Event: {input.Id}");
        Console.WriteLine($"Database: {ctx.Database}");
        Console.WriteLine($"\nJob: state {job.State}, {job.Attempts} attempt(s)" +
            (job.FirstTextMs is null ? "" : $", first text {job.FirstTextMs:F0} ms") +
            (job.ProcessingMs is null ? "" : $", processing {job.ProcessingMs:F0} ms"));
        Console.WriteLine($"  Result: {job.Result}");
        Console.WriteLine("\nChecks:");
        Console.WriteLine("  PASS  Event accepted, processed and completed");
        Console.WriteLine("  PASS  No action was executed (the result is guidance only)\n");

        ConsoleResult.Write(new { status = "passed", example = ctx.Example,
            execution = ctx.Live ? "azure-maf" : "local-fixture", job,
            actionExecuted = false, database = ctx.Database });
    }
}
