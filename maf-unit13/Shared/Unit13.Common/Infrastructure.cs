using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Unit13;

public sealed class Options(string[] args)
{
    public string Mode => args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
        ? args[0] : Environment.GetEnvironmentVariable("SECURITY_MODE") ?? "fixture";
    public string? Get(string name)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0) return null;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("Missing argument value.");
        return args[index + 1];
    }
}

public static class EnvironmentFile
{
    public static string FindRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "unit13.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot locate the complete maf-unit13 package.");
    }
    public static void Load(string path)
    {
        if (!File.Exists(path)) return;
        foreach (string original in File.ReadAllLines(path))
        {
            string line = original.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int split = line.IndexOf('=');
            if (split < 1) throw new FormatException("Invalid .env assignment.");
            string name = line[..split].Trim(), value = line[(split + 1)..].Trim();
            if (!Regex.IsMatch(name, "^[A-Z][A-Z0-9_]*$")) throw new FormatException("Invalid .env name.");
            if (value.Length >= 2 && ((value[0] == '\'' && value[^1] == '\'') || (value[0] == '"' && value[^1] == '"')))
                value = value[1..^1];
            // Values are literal; no variable interpolation or shell evaluation is performed.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)) && value.Length > 0)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
    public static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value && !value.Contains('<')
        ? value : throw new ArgumentException("Missing assigned configuration: " + name);
}

public sealed class Context
{
    public string UnitRoot { get; }
    public string Artifacts { get; }
    public Options Options { get; }
    public Context(string id, string[] args)
    {
        UnitRoot = EnvironmentFile.FindRoot();
        EnvironmentFile.Load(Path.Combine(UnitRoot, ".env"));
        Options = new(args);
        if (Options.Mode is not ("fixture" or "azure")) throw new ArgumentException("Use fixture or azure.");
        Artifacts = Path.Combine(UnitRoot, "artifacts", id,
            $"run-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Artifacts);
    }
    public void Save(string name, object value) => File.WriteAllText(Path.Combine(Artifacts, name), JsonSerializer.Serialize(value, Lab.Json));
    public void FixtureOnly() => Lab.Require(Options.Mode == "fixture", "This local demonstration supports fixture mode. Use v04 or v10 for Azure.");
}

public static class Entry
{
    public static async Task<int> RunAsync(string id, string[] args, Func<Context, Task<int>> run)
    {
        try
        {
            Context context = new(id, args);
            int result = await run(context);
            Lab.Show(new { example = id, exitCode = result, artifacts = context.Artifacts });
            return result;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Execution failed: " + error.GetType().Name);
            return 2;
        }
    }
}

public static class EndpointPolicy
{
    public static Uri Validate(string value, IEnumerable<string> approvedHosts)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme != "https" || !uri.IsDefaultPort ||
            uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            !uri.IdnHost.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase) ||
            !approvedHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Endpoint is outside the approved Azure OpenAI inventory.");
        return uri;
    }
}

public sealed record RetainedItem(string FileName, string Store, DateTimeOffset ExpiresAt, bool LegalHold);
public sealed record RetentionResult(string FileName, string Action);
public static class Retention
{
    public static RetentionResult[] ApplyToOwnedLabFiles(string directory, IEnumerable<RetainedItem> items, DateTimeOffset now)
    {
        string root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        List<RetentionResult> results = [];
        foreach (RetainedItem item in items)
        {
            if (!Regex.IsMatch(item.FileName, "^[a-z0-9-]+\\.json$")) throw new ArgumentException("Invalid owned lab filename.");
            string path = Path.Combine(root, item.FileName);
            if (new FileInfo(path).LinkTarget is not null) throw new IOException("Linked files are not managed by this lab.");
            string action = item.LegalHold ? "keep_legal_hold" : item.ExpiresAt <= now ? "delete_expired" : "keep_current";
            if (action == "delete_expired") File.Delete(path);
            results.Add(new(item.FileName, action));
        }
        return results.ToArray();
    }
}

public static class PythonGate
{
    public static async Task<int> RunAsync(string unitRoot, string reportDirectory)
    {
        ProcessStartInfo info = new(Environment.GetEnvironmentVariable("PYTHON_COMMAND") ?? "python")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Path.Combine(unitRoot, "Python", "security-gate.py"));
        info.ArgumentList.Add(Path.GetFullPath(reportDirectory));
        using Process process = Process.Start(info) ?? throw new IOException("Python could not start.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource budget = new(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(budget.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        Console.Write(await stdout); Console.Error.Write(await stderr);
        return process.ExitCode;
    }
}
