using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maf.Unit15.Application;

public sealed record Evidence(string Service, string Status, string Mode,
    string? RequestId = null, string? TraceId = null);
public sealed record Assessment(
    [property: JsonPropertyName("operation_id")] string OperationId,
    [property: JsonPropertyName("service")] string Service,
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("evidence_mode")] string EvidenceMode);
public sealed record RecordedEvent(string EventId, string EventType, int SchemaVersion,
    string CorrelationId, Assessment Data);
public sealed record PendingEvent(string EventId, string Payload);
public sealed record Delivery(string EventId, string Outcome);
public sealed record ReviewItem(string OperationId, string Service, string State);
public sealed record StoreView(IReadOnlyList<Assessment> Assessments, long PendingEvents,
    long InboxCount, IReadOnlyList<ReviewItem> ReviewItems);

public static class EventContract
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { NumberHandling = JsonNumberHandling.Strict };
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static void Validate(RecordedEvent? e)
    {
        if (e is null || !Guid.TryParse(e.EventId, out _) ||
            e.EventType != "AssessmentRecorded" || e.SchemaVersion != 1 || e.Data is null ||
            string.IsNullOrWhiteSpace(e.Data.OperationId) || e.Data.OperationId.Length > 120 ||
            e.CorrelationId != e.Data.OperationId || e.Data.Service is not ("vpn" or "email") ||
            e.Data.Decision is not ("review_required" or "no_action") ||
            e.Data.EvidenceMode is not ("fixture" or "azure"))
            throw new InvalidDataException("Unsupported or invalid event contract.");
    }
    public static RecordedEvent Parse(string payload)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > 16384)
            throw new InvalidDataException("Event exceeds the bounded contract.");
        try
        {
            var e = JsonSerializer.Deserialize<RecordedEvent>(payload, Json);
            Validate(e);
            return e!;
        }
        catch (JsonException error) { throw new InvalidDataException("Invalid event JSON.", error); }
    }
}

public interface IEvidenceReader
{
    Task<Evidence> ReadAsync(string service, CancellationToken cancellationToken);
}
public interface IAssessmentStore
{
    Task<Assessment?> FindAsync(string operationId, CancellationToken cancellationToken);
    Task<Assessment> CommitAsync(Assessment assessment, RecordedEvent e, CancellationToken cancellationToken);
}
public interface IOutboxStore
{
    Task<IReadOnlyList<PendingEvent>> PendingAsync(CancellationToken cancellationToken);
    Task MarkSentAsync(string eventId, CancellationToken cancellationToken);
}
public interface IEventConsumer
{
    Task<string> ConsumeAsync(string payload, CancellationToken cancellationToken);
}
