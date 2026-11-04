using System.Text.RegularExpressions;
namespace Unit05.Common;
public sealed class ConfigurationException(string message) : Exception(message);

public static class DotEnv
{
    public static string FindRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Maf.Unit05.slnx"))) return dir.FullName;
        throw new ConfigurationException("Extract the complete ZIP, including Maf.Unit05.slnx and the root .env.");
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



public sealed record CourseSettings(string Root, bool Demo, string PromptVersion,
    int TimeoutSeconds, int MaxOutputTokens, int MaxIterations)
{
    public static CourseSettings Load(Arguments args)
    {
        string root = DotEnv.FindRoot();
        DotEnv.Load(Path.Combine(root, ".env"));
        string mode = Setting("MAF_EXECUTION_MODE") ?? "demo";
        if (mode is not ("demo" or "live")) throw new ConfigurationException("MAF_EXECUTION_MODE must be demo or live.");
        string version = args.PromptVersion ?? Setting("PROMPT_VERSION") ?? "v2";
        if (version is not ("v1" or "v2")) throw new ConfigurationException("PROMPT_VERSION must be v1 or v2.");
        return new(root, args.Demo || (!args.Live && mode == "demo"), version,
            Number("REQUEST_TIMEOUT_SECONDS", 45), Number("MAX_OUTPUT_TOKENS", 1600), Number("MAX_ITERATIONS", 4));
    }
    public static string? Setting(string name) => Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } s ? s : null;
    public static string Required(string name) => Setting(name) is { } value && !value.Contains('<') && !value.Contains('>')
        ? value : throw new ConfigurationException($"Set {name} in the shared root .env.");
    private static int Number(string name, int fallback)
    {
        var raw = Setting(name);
        if (raw is null) return fallback;
        return int.TryParse(raw, out var n) && n is >= 1 and <= 100000 ? n
            : throw new ConfigurationException($"{name} must be a positive integer no greater than 100000.");
    }
}

public sealed class Arguments
{
    public bool Demo { get; private set; }
    public bool Live { get; private set; }
    public bool Injection { get; private set; }
    public string? PromptVersion { get; private set; }
    public string? Input { get; private set; }
    public int Repeats { get; private set; } = 1;
    public Arguments(string[] args)
    {
        var words = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--demo": Demo = true; break;
                case "--live": Live = true; break;
                case "--injection": Injection = true; break;
                case "--prompt-version": PromptVersion = Next(); break;
                case "--input": Input = Next(); break;
                case "--repeat":
                    string count = Next();
                    if (!int.TryParse(count, out int n) || n is < 1 or > 3)
                        throw new ConfigurationException("--repeat must be from 1 to 3.");
                    Repeats = n; break;
                default:
                    if (args[i].StartsWith("--")) throw new ConfigurationException("Unknown command-line option.");
                    words.Add(args[i]); break;
            }
            string Next() => i + 1 < args.Length ? args[++i]
                : throw new ConfigurationException("An option value is missing.");
        }
        if (Demo && Live) throw new ConfigurationException("Select either --demo or --live.");
        if (Input is not null && words.Count != 0) throw new ConfigurationException("Use either --input or a positional message.");
        Input ??= words.Count == 0 ? null : string.Join(" ", words);
    }
}
