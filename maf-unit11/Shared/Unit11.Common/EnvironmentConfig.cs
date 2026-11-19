namespace Maf.Unit11;

using System.Text.Json;

public static class EnvironmentConfig
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "Maf.Unit11.slnx")))
                    return directory.FullName;
        throw new InvalidOperationException("Keep the example inside the complete maf-unit11 package.");
    }

    public static void Load()
    {
        string path = Path.Combine(Root, ".env");
        if (!File.Exists(path)) File.Copy(Path.Combine(Root, ".env.example"), path, overwrite: false);
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int equal = line.IndexOf('=');
            if (equal < 1) throw new InvalidDataException("Use NAME=value assignments in the common .env.");
            string name = line[..equal].Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Z][A-Z0-9_]*$"))
                throw new InvalidDataException("Invalid environment variable name.");
            string value = line[(equal + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') ||
                                     (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
                Environment.SetEnvironmentVariable(name, value);
        }
    }

    public static int Number(string name, int fallback, int minimum, int maximum)
    {
        string? raw = Environment.GetEnvironmentVariable(name);
        int value = string.IsNullOrWhiteSpace(raw) ? fallback :
            int.TryParse(raw, out int parsed) ? parsed : throw new InvalidDataException($"Invalid integer: {name}.");
        return value >= minimum && value <= maximum ? value :
            throw new InvalidDataException($"{name} must be between {minimum} and {maximum}.");
    }
}

public sealed class Arguments
{
    private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    public Arguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Arguments must use --name value or --flag.");
            string key = args[i][2..];
            string value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i] : "true";
            if (!values.TryAdd(key, value)) throw new ArgumentException($"Repeated argument: --{key}.");
        }
    }
    public bool Has(string name) => values.ContainsKey(name);
    public string Get(string name, string fallback) => values.GetValueOrDefault(name) ?? fallback;
    public int Number(string name, int fallback) => values.TryGetValue(name, out string? raw)
        ? int.TryParse(raw, out int value) ? value : throw new ArgumentException($"Invalid --{name}.") : fallback;
    public void Allow(params string[] names)
    {
        string[] unknown = values.Keys.Except(names.Concat(["demo", "live", "database"])).ToArray();
        if (unknown.Length != 0) throw new ArgumentException($"Unknown argument: --{unknown[0]}.");
    }
}

public sealed class ExampleContext
{
    public string Example { get; }
    public Arguments Args { get; }
    public bool Live { get; }
    public string DataDirectory { get; }
    public string Database { get; }
    public int Workers => EnvironmentConfig.Number("WORKER_COUNT", 2, 1, 8);
    public int PollMs => EnvironmentConfig.Number("POLL_INTERVAL_MS", 500, 10, 5000);
    public int BudgetSeconds => EnvironmentConfig.Number("REQUEST_TIMEOUT_SECONDS", 45, 1, 120);
    public int LeaseSeconds => EnvironmentConfig.Number("CLAIM_LEASE_SECONDS", 90, BudgetSeconds + 1, 600);
    public int FragmentDelayMs => Args.Number("delay-ms", EnvironmentConfig.Number("FRAGMENT_DELAY_MS", 150, 1, 5000));

    public ExampleContext(string example, Arguments args)
    {
        EnvironmentConfig.Load();
        Example = example; Args = args;
        if (args.Has("demo") && args.Has("live")) throw new ArgumentException("Choose --demo or --live.");
        string mode = args.Has("live") ? "live" : args.Has("demo") ? "demo" :
            Environment.GetEnvironmentVariable("MAF_EXECUTION_MODE") ?? "demo";
        if (mode is not ("demo" or "live")) throw new InvalidDataException("MAF_EXECUTION_MODE must be demo or live.");
        Live = mode == "live";
        DataDirectory = Path.Combine(EnvironmentConfig.Root, ".data", example);
        Directory.CreateDirectory(DataDirectory);
        string configured = args.Get("database", Environment.GetEnvironmentVariable("UNIT11_DB") ?? "");
        Database = string.IsNullOrWhiteSpace(configured) ? Path.Combine(DataDirectory, "queue.db") :
            Path.IsPathRooted(configured) ? configured :
            throw new ArgumentException("Use an absolute --database or UNIT11_DB path.");
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
    }

    public EventStore Store(IIntakePolicy? policy = null, string? database = null)
    {
        var store = new EventStore(database ?? Database, LeaseSeconds, policy);
        store.Initialize();
        return store;
    }
    public IEventReasoner Reasoner() => Live ? new MafReasoner() :
        new FixtureReasoner(FragmentDelayMs, Environment.GetEnvironmentVariable("LAB_FAIL_FIRST") == "1",
            Environment.GetEnvironmentVariable("LAB_POISON_ID"));
    public void RequireDemo()
    {
        if (Live) throw new ArgumentException("This deterministic mechanics example requires --demo.");
    }
    public SupportEvent Event(string prefix, string service = "vpn", string status = "degraded") => new(
        $"EVT-{prefix}-{Guid.NewGuid():N}".ToUpperInvariant(), "ServiceStatusChanged.v1", "training-a",
        Guid.NewGuid().ToString(), DateTimeOffset.UtcNow,
        new(service, status, $"Synthetic observation for {service}."));
}

public static class ConsoleResult
{
    public static void Write(object value) => Console.WriteLine("RESULT " + JsonSerializer.Serialize(value, LabJson.Options));
    public static async Task<int> Run(string example, string[] args, Func<ExampleContext, Task> action)
    {
        try { await action(new ExampleContext(example, new Arguments(args))); return 0; }
        catch (Exception ex)
        {
            Write(new { status = "error", example, errorType = ex.GetType().Name, message = ex.Message });
            return 1;
        }
    }
    public static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
