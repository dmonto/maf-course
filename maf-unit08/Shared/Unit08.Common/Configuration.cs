using System.Text.RegularExpressions;

namespace Unit08.Common;

public sealed class Arguments
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);
    public string Scenario { get; }

    public Arguments(string[] args, string defaultScenario, params string[] extraFlags)
    {
        int i = 0;
        Scenario = args.Length > 0 && !args[0].StartsWith("--") ? args[i++] : defaultScenario;
        if (Scenario is not ("vpn" or "payroll" or "combined"))
            throw new ArgumentException("Use vpn, payroll or combined.");
        HashSet<string> flags = ["--demo", "--live", "--fail-network", "--conflict", "--serial"];
        flags.UnionWith(extraFlags);
        HashSet<string> values = ["--cases", "--network-delay-ms", "--service-delay-ms"];
        for (; i < args.Length; i++)
        {
            string key = args[i];
            if (_values.ContainsKey(key)) throw new ArgumentException("Duplicate option: " + key);
            if (flags.Contains(key)) _values.Add(key, null);
            else if (values.Contains(key) && i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                _values.Add(key, args[++i]);
            else throw new ArgumentException("Unknown option or missing value: " + key);
        }
        if (Has("--demo") && Has("--live")) throw new ArgumentException("Select --demo or --live.");
    }

    public bool Has(string flag) => _values.ContainsKey(flag);
    public string? Get(string key) => _values.GetValueOrDefault(key);
    public RunOptions RunOptions => new(Has("--fail-network"), Has("--conflict"), Has("--serial"));
}

public static class DotEnv
{
    public static string FindRoot()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Maf.Unit08.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Keep the complete maf-unit08 package together.");
    }

    public static void Load(string root)
    {
        string path = Path.Combine(root, ".env");
        if (!File.Exists(path))
        {
            try { File.Copy(Path.Combine(root, ".env.example"), path); }
            catch (IOException) when (File.Exists(path)) { }
        }
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int split = line.IndexOf('=');
            if (split <= 0) throw new ArgumentException("Use NAME=value in the root .env.");
            string key = line[..split].Trim();
            if (!Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("Invalid dotenv key.");
            string value = line[(split + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            // Values are literal strings: no shell execution or variable interpolation.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    public static string? Value(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : null;
    public static string Required(string key) => Value(key) is { } v && !v.Contains("YOUR-") && !v.Contains('<')
        ? v : throw new InvalidOperationException("Set " + key + " in the shared .env.");
    public static int Number(string? value, int fallback, int min, int max) => value is null ? fallback :
        int.TryParse(value, out int n) && n >= min && n <= max ? n :
        throw new ArgumentException($"Expected an integer from {min} to {max}.");
}

public sealed record RuntimeSettings(bool Live, int SpecialistTimeoutSeconds = 30,
    int CoordinatorTimeoutSeconds = 30, int RunTimeoutSeconds = 90,
    int SpecialistTokens = 300, int CoordinatorTokens = 400, int Concurrency = 2,
    int NetworkDelayMs = 150, int ServiceDelayMs = 250)
{
    public static RuntimeSettings Load(Arguments args)
    {
        DotEnv.Load(DotEnv.FindRoot());
        string mode = DotEnv.Value("MAF_EXECUTION_MODE") ?? "demo";
        if (mode is not ("demo" or "live")) throw new ArgumentException("MAF_EXECUTION_MODE must be demo or live.");
        bool live = args.Has("--live") || (!args.Has("--demo") && mode == "live");
        return new(live,
            DotEnv.Number(DotEnv.Value("SPECIALIST_TIMEOUT_SECONDS"), 30, 1, 120),
            DotEnv.Number(DotEnv.Value("COORDINATOR_TIMEOUT_SECONDS"), 30, 1, 120),
            DotEnv.Number(DotEnv.Value("RUN_TIMEOUT_SECONDS"), 90, 1, 600),
            DotEnv.Number(DotEnv.Value("SPECIALIST_MAX_OUTPUT_TOKENS"), 300, 128, 4000),
            DotEnv.Number(DotEnv.Value("COORDINATOR_MAX_OUTPUT_TOKENS"), 400, 128, 4000),
            DotEnv.Number(DotEnv.Value("SPECIALIST_CONCURRENCY"), 2, 1, 2),
            DotEnv.Number(args.Get("--network-delay-ms") ?? DotEnv.Value("NETWORK_DELAY_MS"), 150, 0, 10000),
            DotEnv.Number(args.Get("--service-delay-ms") ?? DotEnv.Value("SERVICE_DELAY_MS"), 250, 0, 10000));
    }
}
