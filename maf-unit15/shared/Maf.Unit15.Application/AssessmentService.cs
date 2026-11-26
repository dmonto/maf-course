using System.Diagnostics;
using Maf.Unit15.Domain;

namespace Maf.Unit15.Application;

public static class Unit15Diagnostics
{
    public static readonly ActivitySource Source = new("Maf.Unit15.Application");
}

public sealed class AssessmentService(IAssessmentStore store, IEvidenceReader evidence)
{
    public async Task<Assessment> AssessAsync(string operationId, string service, CancellationToken ct)
    {
        using var activity = Unit15Diagnostics.Source.StartActivity("assessment");
        activity?.SetTag("operation.id", operationId);
        try
        {
            if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 120 ||
                service is not ("vpn" or "email"))
                throw new ArgumentException("Invalid assessment command.");
            LabConsole.Write("MILESTONE", $"Assess {service} (operationId={operationId}). Step 1: idempotency lookup in the producer database.");
            var existing = await store.FindAsync(operationId, ct);
            if (existing is not null)
            {
                if (existing.Service != service) throw new InvalidOperationException("Operation ID conflict.");
                LabConsole.Write("IDEMPOTENT", $"Operation already committed (decision={existing.Decision}); return it without reading evidence or writing again.");
                return existing;
            }
            // External work finishes before the local database transaction begins.
            LabConsole.Write("MILESTONE", $"Step 2: read evidence through the port ({evidence.GetType().Name}) BEFORE opening any database transaction.");
            Evidence observed = await evidence.ReadAsync(service, ct);
            LabConsole.Write("EVIDENCE", $"service={observed.Service} status={observed.Status} mode={observed.Mode}");
            if (observed.Service != service || observed.Mode is not ("fixture" or "azure"))
                throw new InvalidDataException("Evidence identity or mode mismatch.");
            var result = new Assessment(operationId, service, AssessmentPolicy.Decide(observed.Status), observed.Mode);
            LabConsole.Write("POLICY", $"Domain rule: status '{observed.Status}' => decision '{result.Decision}' (pure function, no I/O).");
            var e = new RecordedEvent(Guid.NewGuid().ToString(), "AssessmentRecorded", 1, operationId, result);
            LabConsole.Write("SQL", "Step 3: ONE transaction writes the assessment row AND the AssessmentRecorded event into the outbox table.");
            return await store.CommitAsync(result, e, ct);
        }
        catch { activity?.SetStatus(ActivityStatusCode.Error); throw; }
    }
}

public sealed class OutboxDispatcher(IOutboxStore outbox, IEventConsumer consumer)
{
    public async Task<IReadOnlyList<Delivery>> DispatchAsync(bool failAfterDelivery, CancellationToken ct)
    {
        using var activity = Unit15Diagnostics.Source.StartActivity("delivery");
        var results = new List<Delivery>();
        var pendingEvents = await outbox.PendingAsync(ct);
        LabConsole.Write("OUTBOX", $"Dispatcher found {pendingEvents.Count} unsent event(s) (SELECT ... WHERE sent=0). Failure injection={failAfterDelivery}.");
        foreach (var pending in pendingEvents)
        {
            LabConsole.Write("OUTBOX", $"Deliver event {pending.EventId} to {consumer.GetType().Name}.");
            string outcome = await consumer.ConsumeAsync(pending.Payload, ct);
            LabConsole.Write("OUTBOX", $"Consumer outcome: {outcome}.");
            results.Add(new Delivery(pending.EventId, outcome));
            if (failAfterDelivery)
            {
                LabConsole.Write("FAULT", "Injected crash AFTER the consumer committed but BEFORE the outbox row is marked sent.");
                throw new IOException("Injected crash after consumer commit");
            }
            await outbox.MarkSentAsync(pending.EventId, ct);
            LabConsole.Write("SQL", "UPDATE outbox SET sent=1 (event acknowledged).");
        }
        return results;
    }
}
