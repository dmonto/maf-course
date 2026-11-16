using System.Collections.Concurrent;
using System.Text.Json;

namespace Unit08.Common;

public sealed record AuditEntry(DateTimeOffset Time, string RunId, string Event, string Role, object? Detail);

public sealed class AuditTrail(bool writeToConsole = true)
{
    private readonly ConcurrentQueue<AuditEntry> _events = new();
    private readonly object _gate = new();
    public AuditEntry[] Events => _events.ToArray();
    public void Emit(string runId, string name, string role, object? detail = null)
    {
        lock (_gate)
        {
            var entry = new AuditEntry(DateTimeOffset.UtcNow, runId, name, role, detail);
            _events.Enqueue(entry);
            if (writeToConsole) Console.Error.WriteLine(JsonSerializer.Serialize(entry));
        }
    }
}

public sealed class ExecutionMetrics
{
    private int _active, _peak, _analyses, _syntheses;
    public int PeakSpecialistConcurrency => Volatile.Read(ref _peak);
    public int SpecialistInvocations => Volatile.Read(ref _analyses);
    public int SynthesisInvocations => Volatile.Read(ref _syntheses);

    internal void EnterSpecialist()
    {
        Interlocked.Increment(ref _analyses);
        int active = Interlocked.Increment(ref _active);
        int peak;
        while (active > (peak = Volatile.Read(ref _peak))
            && Interlocked.CompareExchange(ref _peak, active, peak) != peak) { }
    }
    internal void LeaveSpecialist() => Interlocked.Decrement(ref _active);
    internal void EnterSynthesis() => Interlocked.Increment(ref _syntheses);
}
