using System.Globalization;
using System.Text.RegularExpressions;

namespace Unit12.Common;

public static class UnitEnvironment
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Maf.Unit12.slnx"))) return d.FullName;
        throw new InvalidOperationException("Keep the complete maf-unit12 directory, including Maf.Unit12.slnx.");
    }

    public static void Load()
    {
        string path = Path.Combine(Root, ".env");
        if (!File.Exists(path)) return;
        foreach (string line in File.ReadAllLines(path))
        {
            string text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#')) continue;
            int eq = text.IndexOf('=');
            if (eq < 1) throw new InvalidOperationException("Use literal KEY=value assignments in the shared .env.");
            string key = text[..eq].Trim();
            if (!Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new InvalidOperationException("An invalid variable name was found in the shared .env.");
            string value = text[(eq + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') ||
                (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    public static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value) || value.Contains('<') || value.StartsWith("YOUR-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Set the assigned {name} in the shared .env or process environment.");
        return value;
    }

    public static string? Optional(string name) => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
        ? null : Environment.GetEnvironmentVariable(name);

    public static Uri Endpoint()
    {
        if (!Uri.TryCreate(Required("AZURE_OPENAI_ENDPOINT"), UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != "https" || uri.AbsolutePath != "/" || uri.Query.Length != 0 ||
            uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 || !uri.IsDefaultPort ||
            !uri.Host.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use the assigned Azure OpenAI resource-root HTTPS endpoint.");
        return uri;
    }

    public static int Repetitions()
    {
        if (!int.TryParse(Optional("EVAL_REPETITIONS") ?? "1", NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n is < 1 or > 5)
            throw new ArgumentException("EVAL_REPETITIONS must be an integer from 1 to 5.");
        return n;
    }
}

public sealed record CliOptions(string Mode, IReadOnlyDictionary<string, string> Values)
{
    public string? Get(string name) => Values.GetValueOrDefault(name);
    public static CliOptions Parse(string[] args, params string[] allowed)
    {
        string mode = UnitEnvironment.Optional("EVAL_MODE") ?? "fixture";
        bool modeSet = false;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (token is "fixture" or "fixture-bad" or "azure" or "--azure" || token == "--mode")
            {
                if (modeSet) throw new ArgumentException("Specify the execution mode once.");
                modeSet = true;
                mode = token == "--azure" ? "azure" : token == "--mode" && ++i < args.Length ? args[i] : token;
                continue;
            }
            if (!token.StartsWith("--", StringComparison.Ordinal) || !allowed.Contains(token[2..]) ||
                ++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(token[2..], args[i]))
                throw new ArgumentException("Unknown, missing or duplicate option.");
        }
        if (mode is not ("fixture" or "fixture-bad" or "azure")) throw new ArgumentException("Use fixture, fixture-bad or azure.");
        return new(mode, values);
    }
}

public static class App
{
    public static async Task<int> RunAsync(Func<Task<int>> body)
    {
        try { UnitEnvironment.Load(); return await body(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FileNotFoundException)
        { Console.Error.WriteLine(ex.Message); return 2; }
        catch (Exception ex)
        { Console.Error.WriteLine($"Execution is inconclusive: {ex.GetType().Name}."); return 2; }
    }

    public static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
