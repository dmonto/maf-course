using System.Text.Json;

namespace Unit16.Common;

public static class JsonOutput
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        { WriteIndented = true };
    public static string Text(object? value) => JsonSerializer.Serialize(value, Options);
}

public static class LabConsole
{
    public static void Write(string kind, string text) => Console.WriteLine($"[{kind}] {text}");
    public static void Data(string kind, object? value) => Write(kind, JsonOutput.Text(value));
    public static void Check(string name, bool passed)
    {
        Write("CHECK", $"{(passed ? "PASS" : "FAIL")} | {name}");
        if (!passed) throw new InvalidOperationException(name);
    }
}

public sealed class LabArguments(string[] args)
{
    public bool Has(string flag) => args.Contains(flag, StringComparer.Ordinal);
    public string? Value(string flag)
    {
        int index = Array.IndexOf(args, flag);
        if (index < 0) return null;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"A value is required after {flag}.");
        return args[index + 1];
    }
    public void Validate()
    {
        string[] flags = ["--live", "--http", "--require-azure", "--inject-regression", "--base", "--report"];
        for (int i = 0; i < args.Length; i++)
        {
            if (!flags.Contains(args[i], StringComparer.Ordinal))
                throw new ArgumentException($"Unsupported argument: {args[i]}");
            if (args[i] is "--base" or "--report") { _ = Value(args[i]); i++; }
        }
        if (Has("--http") && Has("--live"))
            throw new ArgumentException("Choose --live for direct Azure MAF or --http for a host.");
        if (Has("--require-azure") && !Has("--http"))
            throw new ArgumentException("--require-azure requires --http.");
        if (Value("--base") is not null && !Has("--http"))
            throw new ArgumentException("--base requires --http.");
        if (Has("--inject-regression") && (Has("--live") || Has("--http")))
            throw new ArgumentException("Regression injection is available only with the local fixture model.");
    }
}

public static class UnitSettings
{
    public static string Root { get; } = FindRoot();
    public static void Load()
    {
        string path = Path.Combine(Root, ".env");
        if (!File.Exists(path)) path = Path.Combine(Root, ".env.example");
        foreach (string line in File.ReadAllLines(path))
        {
            string clean = line.Trim();
            if (clean.Length == 0 || clean.StartsWith('#')) continue;
            int split = clean.IndexOf('=');
            if (split <= 0) throw new InvalidDataException("Invalid environment setting.");
            string key = clean[..split].Trim();
            string value = clean[(split + 1)..].Trim().Trim('"', '\'');
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
        LabConsole.Write("CONFIG", $"Shared environment file: {path}. Process values take precedence.");
    }
    public static string Required(string key)
    {
        string? value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value) || value.Contains('<'))
            throw new InvalidOperationException($"Set the assigned {key} in the shared environment.");
        return value;
    }
    public static string Value(string key, string fallback) =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)) ? fallback :
            Environment.GetEnvironmentVariable(key)!;
    public static string ReportPath(string example, LabArguments args)
    {
        string? supplied = args.Value("--report");
        string path = supplied is null
            ? Path.Combine(Root, "artifacts", $"{example}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json")
            : Path.GetFullPath(supplied);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }
    private static string FindRoot()
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "unit16.manifest.json"))) return dir.FullName;
        throw new DirectoryNotFoundException("Run the supplied project from maf-course/maf-unit16.");
    }
}
