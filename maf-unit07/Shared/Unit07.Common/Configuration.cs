using System.Text.RegularExpressions;

namespace Unit07.Common;

public sealed class Arguments
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);
    public string Command { get; }
    public string Service { get; }
    public Arguments(string[] args, ExampleProfile profile, string[] extraFlags)
    {
        int i = 0;
        Command = args.Length > 0 && !args[0].StartsWith("--") ? args[i++] : profile.DefaultCommand;
        if (Command is not ("run" or "pause" or "resume")) throw new ArgumentException("Use run, pause or resume.");
        Service = i < args.Length && !args[i].StartsWith("--") ? args[i++] : profile.DefaultService;
        HashSet<string> flags = ["--live", "--demo", "--fail", "--serial", "--reset"];
        flags.UnionWith(extraFlags);
        HashSet<string> values = ["--state", "--decision", "--case", "--symptom", "--status-delay-ms", "--guide-delay-ms"];
        for (; i < args.Length; i++)
        {
            string key = args[i];
            if (_values.ContainsKey(key)) throw new ArgumentException("Duplicate option: " + key);
            if (flags.Contains(key)) _values.Add(key, null);
            else if (values.Contains(key) && i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                _values.Add(key, args[++i]);
            else throw new ArgumentException("Unknown option or missing value: " + key);
        }
        if (Has("--live") && Has("--demo")) throw new ArgumentException("Select --demo or --live.");
        if (Get("--decision") is { } decision && decision is not ("yes" or "no"))
            throw new ArgumentException("Use --decision yes or --decision no for synthetic reviews.");
        if (Command == "pause" && Get("--decision") is not null)
            throw new ArgumentException("A paused run must leave its review unanswered.");
        if (!Regex.IsMatch(State, "^[a-zA-Z0-9-]{1,50}$")) throw new ArgumentException("Invalid --state identifier.");
    }
    public bool Has(string option) => _values.ContainsKey(option);
    public string? Get(string option) => _values.GetValueOrDefault(option);
    public string State => Get("--state") ?? "default";
}

public static class DotEnv
{
    public static string FindRoot()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Maf.Unit07.slnx"))) return dir.FullName;
        throw new InvalidOperationException("Keep the complete maf-unit07 package together.");
    }
    public static void Load(string root)
    {
        string path = Path.Combine(root, ".env");
        if (!File.Exists(path)) File.Copy(Path.Combine(root, ".env.example"), path);
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int split = line.IndexOf('=');
            if (split <= 0) throw new ArgumentException("Use NAME=value in the root .env.");
            string key = line[..split].Trim();
            if (!Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("Invalid dotenv key.");
            string value = line[(split + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') ||
                (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            // Values are literal strings. No shell execution or interpolation occurs.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }
    public static string? Value(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
    public static string Required(string name) => Value(name) is { } value && !value.Contains("YOUR-")
        ? value : throw new InvalidOperationException("Set " + name + " in the root .env.");
    public static int Number(string? value, int fallback, int min, int max) => value is null ? fallback :
        int.TryParse(value, out int number) && number >= min && number <= max ? number :
        throw new ArgumentException($"Expected an integer from {min} to {max}.");
}

public sealed record ExampleProfile(string Id, string DefaultService = "vpn",
    string DefaultCommand = "run", bool PrintGraph = false, bool VerifyOrder = false,
    bool KeepFailedBookmark = false);

public sealed record RuntimeOptions(string Root, string DataDirectory, bool Live, Arguments Arguments,
    int TimeoutSeconds, int MaxOutputTokens, int StatusDelayMs, int GuideDelayMs, string ConfigurationKey)
{
    public static RuntimeOptions Load(Arguments args, ExampleProfile profile)
    {
        string root = DotEnv.FindRoot();
        DotEnv.Load(root);
        string mode = DotEnv.Value("MAF_EXECUTION_MODE") ?? "demo";
        if (mode is not ("demo" or "live")) throw new ArgumentException("MAF_EXECUTION_MODE must be demo or live.");
        bool live = args.Has("--live") || (!args.Has("--demo") && mode == "live");
        string config = $"support-v1/maf-1.24.0/{profile.Id}/{(live ? "live" : "demo")}";
        if (live) config += "/" + DotEnv.Required("AZURE_OPENAI_ENDPOINT") + "/" + DotEnv.Required("AZURE_OPENAI_DEPLOYMENT_NAME");
        string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(config)));
        return new(root, Path.Combine(root, "data", profile.Id, args.State), live, args,
            DotEnv.Number(DotEnv.Value("REQUEST_TIMEOUT_SECONDS"), 45, 1, 120),
            DotEnv.Number(DotEnv.Value("MAX_OUTPUT_TOKENS"), 400, 128, 4000),
            DotEnv.Number(args.Get("--status-delay-ms") ?? DotEnv.Value("STATUS_DELAY_MS"), 200, 1, 5000),
            DotEnv.Number(args.Get("--guide-delay-ms") ?? DotEnv.Value("GUIDE_DELAY_MS"), 350, 1, 5000), key);
    }
}
