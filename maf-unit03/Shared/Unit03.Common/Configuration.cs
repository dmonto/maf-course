using System.Globalization;
using System.Text.RegularExpressions;

namespace Unit03.Common;

public sealed class ConfigurationException(string message) : Exception(message);

public static class DotEnv
{
    public static string FindRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Maf.Unit03.slnx"))) return dir.FullName;
        throw new ConfigurationException("Extract the complete ZIP, including Maf.Unit03.slnx and the root .env.");
    }

    public static void Load(string path)
    {
        if (!File.Exists(path))
            throw new ConfigurationException("Copy the root .env.example to .env, then edit that shared file.");
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int split = line.IndexOf('=');
            if (split <= 0) throw new ConfigurationException("Use NAME=value in the root .env file.");
            string name = line[..split].Trim();
            if (!Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ConfigurationException("Invalid environment variable name in .env.");
            string value = line[(split + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            // A nonempty process value deliberately overrides the shared file.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}

public sealed class Arguments
{
    readonly HashSet<string> flags = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    public Arguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string name = args[i];
            if (name is "--demo" or "--live" or "--interactive") { flags.Add(name); continue; }
            if (name is not ("--case" or "--input") || i + 1 >= args.Length)
                throw new ConfigurationException("Use --demo, --live, --interactive, --case VALUE or --input TEXT.");
            values[name] = args[++i];
        }
        if (Has("--demo") && Has("--live"))
            throw new ConfigurationException("Select either --demo or --live.");
    }
    public bool Has(string name) => flags.Contains(name);
    public string? Get(string name) => values.GetValueOrDefault(name);
}

public sealed record RunLimits(int MaxInputCharacters = 2000, int MaxToolAttempts = 3,
    int MaxIterations = 4, int MaxOutputTokens = 800, int MaxTurns = 8, int TimeoutSeconds = 45);

public sealed class CourseContext
{
    public string Root { get; }
    public Arguments Args { get; }
    public bool Demo { get; }
    public RunLimits Limits { get; }
    public CourseContext(string[] args)
    {
        Root = DotEnv.FindRoot();
        DotEnv.Load(Path.Combine(Root, ".env"));
        Args = new(args);
        string mode = Setting("MAF_EXECUTION_MODE") ?? "demo";
        if (mode is not ("demo" or "live")) throw new ConfigurationException("MAF_EXECUTION_MODE must be demo or live.");
        Demo = Args.Has("--demo") || (!Args.Has("--live") && mode == "demo");
        Limits = new(2000, Number("MAX_TOOL_ATTEMPTS", 3), Number("MAX_ITERATIONS", 4),
            Number("MAX_OUTPUT_TOKENS", 800), Number("MAX_CONVERSATION_TURNS", 8),
            Number("REQUEST_TIMEOUT_SECONDS", 45));
        Console.WriteLine(Demo
            ? "DEMO MODE: a scripted synthetic model drives the real MAF pipeline. No Azure calls or measured token usage."
            : "LIVE MODE: model requests use the assigned Azure deployment. Local-only examples still make no inference calls.");
    }
    public string? Setting(string name) => Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } s ? s : null;
    public string Required(string name) => Setting(name) is { } value && !value.Contains('<') && !value.Contains('>')
        ? value : throw new ConfigurationException($"Set {name} in the shared root .env.");
    int Number(string name, int fallback)
    {
        string? value = Setting(name);
        if (value is null) return fallback;
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n is < 1 or > 100000)
            throw new ConfigurationException($"{name} must be a positive integer no greater than 100000.");
        return n;
    }
    public void Save(string name, object report)
    {
        string path = Path.Combine(Root, "output", name + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(report,
            new System.Text.Json.JsonSerializerOptions(Audit.JsonOptions) { WriteIndented = true }));
        Console.WriteLine($"Report: {Path.GetRelativePath(Root, path)}");
    }
}

public static class CourseApp
{
    public static async Task<int> RunAsync(string[] args, Func<CourseContext, Task> action)
    {
        try { await action(new CourseContext(args)); return 0; }
        catch (Exception ex)
        {
            Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                status = "error", errorType = ex.GetType().Name,
                message = ex is ConfigurationException ? ex.Message : "Check configuration, package versions and model access."
            }));
            return 1;
        }
    }
}
