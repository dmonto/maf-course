using Unit06.Common;
using Unit06.Exercise01;

return await ConsoleApp.RunAsync(args, async (settings, options) =>
{
    string? confirmed = options.Get("--confirm-os");
    if (confirmed is not null && confirmed is not ("Windows 11" or "Windows 10" or "Linux" or "macOS"))
        throw new ConfigurationException("Use a permitted operating system for --confirm-os.");
    var policy = new TurnPolicy<ConfirmedCaseState>("exercise01-confirmed-os-v1",
        () => new("INC-DEMO-001", "vpn", "investigating"))
    {
        // This host option is an explicit confirmation fixture. The model never sets it.
        PrepareState = state => confirmed is null ? state : state with
        {
            ReportedOperatingSystem = confirmed, Provenance = "user-confirmed",
            ConfirmedUtc = DateTimeOffset.UtcNow
        },
        CreateTools = (actor, state, trace) => new ConfirmedCaseTools(actor, state, trace).CreateTools(),
        AdditionalInstructions = "For the recorded operating system call GetCaseContext. " +
            "Describe its provenance as user-confirmed, not a verified device measurement."
    };
    string id = options.Session("ex01-confirmed-" + settings.ModeSuffix);
    var engine = new TurnEngine(settings);
    if (options.Has("--audit")) return (object)new { sessionId = id,
        execution = "local-no-inference", audit = engine.Store.ReadAudit(Lab.Actor, id) };
    return await engine.ExecuteAsync(id, options.Input(
        "What is the recorded operating system for my case?"), policy);
}, extraValues: ["--confirm-os"]);
