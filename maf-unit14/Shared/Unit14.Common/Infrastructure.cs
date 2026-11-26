using System.Text.Json;
using System.Text.RegularExpressions;

namespace Unit14;

public static class Lab
{
    public const string PromptVersion = "status-v1";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    public static void Save(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Json) + "\n");
    }
    public static string Value(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
}

// Didactic console trace, same [KIND] text style as Unit 16.
public static class LabConsole
{
    public static void Write(string kind, string text) => Console.WriteLine($"[{kind}] {text}");
    public static void Check(string name, bool passed)
    {
        Write("CHECK", $"{(passed ? "PASS" : "FAIL")} | {name}");
        if (!passed) throw new InvalidOperationException(name);
    }
}

public static class EnvironmentFile
{
    public static string? FindRoot(bool required = true)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "unit14.json"))) return directory.FullName;
        if (required) throw new DirectoryNotFoundException("Keep the complete maf-unit14 directory together.");
        return null;
    }
    public static void LoadWhenAvailable()
    {
        if (FindRoot(false) is { } root) Load(Path.Combine(root, ".env"));
    }
    public static void Load(string path)
    {
        if (!File.Exists(path)) return;
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int split = line.IndexOf('=');
            if (split < 1) throw new FormatException("Invalid .env assignment.");
            string key = line[..split].Trim(), value = line[(split + 1)..].Trim();
            if (!Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new FormatException("Invalid .env name.");
            if (value.Length >= 2 && ((value[0] == '\'' && value[^1] == '\'') || (value[0] == '"' && value[^1] == '"')))
                value = value[1..^1];
            // Values are not interpolated and no shell command is evaluated.
            if (value.Length > 0 && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }
    public static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value && !value.Contains('<')
            ? value : throw new ArgumentException("Load the assigned value for " + name);
}

public sealed class Context
{
    public string Root { get; } = EnvironmentFile.FindRoot()!;
    public string Artifacts { get; }
    public string Mode { get; }
    public string Release { get; }
    public string[] Args { get; }
    public Context(string id, string[] args)
    {
        EnvironmentFile.Load(Path.Combine(Root, ".env"));
        Args = args;
        int index = Array.IndexOf(args, "--mode");
        if (index >= 0 && index + 1 >= args.Length) throw new ArgumentException("Missing mode.");
        Mode = index >= 0 ? args[index + 1] : Lab.Value("Runner__Mode", "fixture");
        if (Mode is not ("fixture" or "azure")) throw new ArgumentException("Use fixture or azure.");
        Release = Lab.Value("Runner__Release", "14.1.0");
        Artifacts = Path.Combine(Root, "artifacts", id,
            $"run-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Artifacts);
    }
    public string Save(string name, object value)
    {
        string path = Path.Combine(Artifacts, name);
        Lab.Save(path, value);
        LabConsole.Write("ARTIFACT", path);
        return path;
    }
    public void FixtureOnly() => Lab.Require(Mode == "fixture", "This example is local-only; use v01 or v05 for Azure-mode HTTP probes.");
}

public static class Entry
{
    public static async Task<int> RunAsync(string id, string[] args, Func<Context, Task> action)
    {
        try
        {
            var context = new Context(id, args);
            LabConsole.Write("START", $"Unit 14 {id} | mode={context.Mode} | release={context.Release} | .NET");
            LabConsole.Write("MILESTONE", $"Artifacts directory: {context.Artifacts}");
            await action(context);
            LabConsole.Write("CONCLUSION", $"{id} completed. Review the milestones, artifacts and checks printed above.");
            Console.WriteLine("RESULT " + JsonSerializer.Serialize(new { example = id, passed = true, artifacts = context.Artifacts }));
            return 0;
        }
        catch (Exception error)
        {
            LabConsole.Write("FAILURE", $"{error.GetType().Name}: {error.Message}");
            LabConsole.Write("CONCLUSION", "The example did not qualify. No success is inferred from a failure.");
            Console.Error.WriteLine($"FAIL {id}: {error.GetType().Name}: {error.Message}");
            return 1;
        }
    }
}

public sealed record ProbeResult(int ExitCode, string ReportPath, JsonElement Report);
public static class HttpProbe
{
    public static Task<ProbeResult> RunAsync(Context context, string address, string release,
        string mode, string name = "smoke.json", int requests = 4, int parallel = 1) =>
        DeploymentProbe.RunAsync(new ProbeOptions(address, release, mode, requests, parallel,
            Output: Path.Combine(context.Artifacts, name)));
}
