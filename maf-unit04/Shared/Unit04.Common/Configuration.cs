using System.Text.RegularExpressions;
namespace Unit04.Common;
public sealed class ConfigurationException(string message) : Exception(message);

public static class DotEnv
{
    public static string FindRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Maf.Unit04.slnx"))) return dir.FullName;
        throw new ConfigurationException("Extract the complete ZIP, including Maf.Unit04.slnx and the root .env.");
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


public sealed record CourseSettings(string Root, bool Demo, int Port, string PromptVersion,
    int TimeoutSeconds, int MaxOutputTokens, int MaxIterations, int MaxTurns, int MaxConversations)
{
    public static CourseSettings Load(string[] args)
    {
        string root = DotEnv.FindRoot();
        DotEnv.Load(Path.Combine(root, ".env"));
        bool demoFlag = false, liveFlag = false;
        string? port = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--demo") demoFlag = true;
            else if (args[i] == "--live") liveFlag = true;
            else if (args[i] == "--port" && i + 1 < args.Length) port = args[++i];
            else throw new ConfigurationException("Use --demo, --live or --port NUMBER.");
        }
        if (demoFlag && liveFlag) throw new ConfigurationException("Select either --demo or --live.");
        string mode = Setting("MAF_EXECUTION_MODE") ?? "demo";
        if (mode is not ("demo" or "live")) throw new ConfigurationException("MAF_EXECUTION_MODE must be demo or live.");
        string version = Setting("Agent__PromptVersion") ?? "v1";
        if (version is not ("v1" or "v2")) throw new ConfigurationException("Agent__PromptVersion must be v1 or v2.");
        return new(root, demoFlag || (!liveFlag && mode == "demo"),
            Number(port ?? Setting("COURSE_PORT"), "COURSE_PORT", 5050, 0, 65535), version,
            Number(Setting("REQUEST_TIMEOUT_SECONDS"), "REQUEST_TIMEOUT_SECONDS", 45),
            Number(Setting("MAX_OUTPUT_TOKENS"), "MAX_OUTPUT_TOKENS", 800),
            Number(Setting("MAX_ITERATIONS"), "MAX_ITERATIONS", 4),
            Number(Setting("MAX_CONVERSATION_TURNS"), "MAX_CONVERSATION_TURNS", 6),
            Number(Setting("MAX_RETAINED_CONVERSATIONS"), "MAX_RETAINED_CONVERSATIONS", 20));
    }
    public static string? Setting(string name) => Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } value ? value : null;
    public static int Number(string? text, string name, int fallback, int min = 1, int max = 100000)
    {
        if (text is null) return fallback;
        return int.TryParse(text, out int value) && value >= min && value <= max
            ? value : throw new ConfigurationException($"{name} must be between {min} and {max}.");
    }
    public static string Required(string name) => Setting(name) is { } value && !value.Contains('<') && !value.Contains('>')
        ? value : throw new ConfigurationException($"Set {name} in the shared root .env.");
}
