using System.Text.RegularExpressions;

namespace Unit06.Common;

public sealed class Arguments
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);
    public Arguments(string[] args, string[]? extraValues = null, string[]? extraFlags = null)
    {
        var valueOptions = new HashSet<string>(["--session", "--input", "--replica"]);
        var flagOptions = new HashSet<string>(["--demo", "--live", "--audit"]);
        valueOptions.UnionWith(extraValues ?? []);
        flagOptions.UnionWith(extraFlags ?? []);
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (_values.ContainsKey(option)) throw new ConfigurationException("Duplicate option.");
            if (flagOptions.Contains(option)) _values.Add(option, null);
            else if (valueOptions.Contains(option) && i + 1 < args.Length &&
                     !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                _values.Add(option, args[++i]);
            else throw new ConfigurationException("Unknown option or missing option value: " + option);
        }
        if (Has("--demo") && Has("--live"))
            throw new ConfigurationException("Select either --demo or --live.");
        if (Get("--session") is { } id && !Regex.IsMatch(id, "^[a-z0-9-]{1,40}$"))
            throw new MemoryFault("invalid-session-id");
        if (Get("--input") is { } text && (text.Trim().Length is 0 or > 1000))
            throw new MemoryFault("invalid-input");
        if (Get("--replica") is { } replica && !Regex.IsMatch(replica, "^[a-zA-Z0-9-]{1,30}$"))
            throw new ConfigurationException("Use a short alphanumeric replica name.");
    }
    public bool Has(string option) => _values.ContainsKey(option);
    public string? Get(string option) => _values.GetValueOrDefault(option);
    public string Session(string fallback) => Get("--session") ?? fallback;
    public string Input(string fallback) => Get("--input") ?? fallback;
}

public static class DotEnv
{
    public static string FindRoot()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Maf.Unit06.slnx"))) return dir.FullName;
        throw new ConfigurationException("Keep the complete maf-unit06 package together.");
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
            if (split <= 0) throw new ConfigurationException("Use NAME=value in the root .env.");
            string name = line[..split].Trim();
            if (!Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ConfigurationException("Invalid environment variable name.");
            string value = line[(split + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') ||
                (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            // Values are literal strings. No shell or expression evaluation occurs.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}

public sealed record CourseSettings(string Root, bool Demo, int TimeoutSeconds,
    int MaxOutputTokens, int MaxIterations, string DatabasePath)
{
    public string Execution => Demo ? "demo-scripted-model" : "azure-openai-live";
    public string ModeSuffix => Demo ? "demo" : "live";
    public static string? Setting(string name) =>
        Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } s ? s : null;
    public static CourseSettings Load(Arguments args)
    {
        string root = DotEnv.FindRoot();
        DotEnv.Load(root);
        string mode = Setting("MAF_EXECUTION_MODE") ?? "demo";
        if (mode is not ("demo" or "live"))
            throw new ConfigurationException("MAF_EXECUTION_MODE must be demo or live.");
        bool demo = args.Has("--demo") || (!args.Has("--live") && mode == "demo");
        string db = Setting("MAF_MEMORY_DATABASE") ?? "data/memory.db";
        db = Path.IsPathRooted(db) ? db : Path.Combine(root, db);
        return new(root, demo, Number("REQUEST_TIMEOUT_SECONDS", 45, 1, 120),
            Number("MAX_OUTPUT_TOKENS", 800, 128, 4000),
            Number("MAX_ITERATIONS", 3, 1, 8), Path.GetFullPath(db));
    }
    private static int Number(string key, int fallback, int min, int max)
    {
        string? raw = Setting(key);
        if (raw is null) return fallback;
        return int.TryParse(raw, out int value) && value >= min && value <= max ? value :
            throw new ConfigurationException($"{key} must be an integer from {min} to {max}.");
    }
    public static string Required(string name) =>
        Setting(name) is { } value && !value.Contains('<') && !value.Contains('>')
            ? value : throw new ConfigurationException($"Set {name} in the root .env.");
}

public static class ConsoleApp
{
    public static async Task<int> RunAsync(string[] args,
        Func<CourseSettings, Arguments, Task<object>> run,
        string[]? extraValues = null, string[]? extraFlags = null)
    {
        try
        {
            var parsed = new Arguments(args, extraValues, extraFlags);
            var settings = CourseSettings.Load(parsed);
            JsonOutput.Write(await run(settings, parsed));
            return 0;
        }
        catch (Exception ex)
        {
            string code = ex switch
            {
                MemoryFault fault => fault.Code,
                ConfigurationException => "configuration-error",
                OperationCanceledException => "timeout",
                Microsoft.Data.Sqlite.SqliteException => "storage-failed",
                _ => "execution-failed"
            };
            Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                outcome = "not-committed", code, errorType = ex.GetType().Name,
                message = ex is ConfigurationException ? ex.Message : null
            }, JsonOutput.Options));
            return code == "version-conflict" ? 3 : 1;
        }
    }
}
