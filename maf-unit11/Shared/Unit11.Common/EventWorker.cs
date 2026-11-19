namespace Maf.Unit11;

using System.Diagnostics;
using System.Text;
using System.ClientModel;

public sealed class EventWorker(EventStore store, IEventReasoner reasoner,
    ILogger<EventWorker> logger, int workers = 2, int pollMs = 500,
    int budgetSeconds = 45, int maxResultChars = 4000) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken ct) =>
        Task.WhenAll(Enumerable.Range(0, workers).Select(_ => LoopAsync(ct)));

    private async Task LoopAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            Claim? claim;
            try { claim = store.TryClaim(); }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
            {
                logger.LogWarning("Store busy; next poll will retry");
                await Task.Delay(pollMs, stopping);
                continue;
            }
            if (claim is null) { await Task.Delay(pollMs, stopping); continue; }
            await ProcessAsync(claim, stopping);
        }
    }

    private async Task ProcessAsync(Claim claim, CancellationToken stopping)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["event_id"] = claim.Event.Id, ["correlation_id"] = claim.Event.CorrelationId,
            ["attempt"] = claim.Attempt
        });
        using var activity = new Activity("unit11.process").Start();
        logger.LogInformation("Started; event_age_ms={Age}", LabConfig.NowMs - claim.AcceptedMs);
        var watch = Stopwatch.StartNew();
        double? firstText = null;
        var text = new StringBuilder();
        int fragments = 0;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        budget.CancelAfter(TimeSpan.FromSeconds(budgetSeconds));
        try
        {
            await reasoner.RunAsync(claim.Event, claim.Attempt, part =>
            {
                firstText ??= watch.Elapsed.TotalMilliseconds;
                if (text.Length + part.Length > maxResultChars)
                    throw new InvalidDataException("Generated explanation exceeded the bound.");
                text.Append(part);
                logger.LogInformation("Text fragment; sequence={Sequence}; chars={Chars}; first_text_ms={First}",
                    ++fragments, part.Length, firstText);
            }, budget.Token);
            if (string.IsNullOrWhiteSpace(text.ToString()))
                throw new InvalidDataException("Empty explanation.");
            bool saved = store.Complete(claim, text.ToString(), firstText, watch.Elapsed.TotalMilliseconds);
            logger.LogInformation("Terminal state={State}; processing_ms={Processing}; total_ms={Total}",
                saved ? "Completed" : "LeaseLost", watch.Elapsed.TotalMilliseconds,
                LabConfig.NowMs - claim.AcceptedMs);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Leave the lease for recovery; incomplete text is never stored as a result.
            logger.LogInformation("Host stopped; event remains recoverable after lease expiry");
        }
        catch (Exception ex)
        {
            bool transient = ex is HttpRequestException or OperationCanceledException ||
                ex is ClientResultException { Status: 408 or 429 or >= 500 };
            string state = store.Fail(claim, transient, ex.GetType().Name);
            logger.LogWarning("Attempt failed; state={State}; code={Code}; processing_ms={Processing}",
                state, ex.GetType().Name, watch.Elapsed.TotalMilliseconds);
        }
    }
}
