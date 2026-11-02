using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Unit02.Common;

public sealed class ConfigurationException(string message) : Exception(message);

public static class DotEnv
{
    public static string FindRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))
                    && File.Exists(Path.Combine(dir.FullName, ".env"))) return dir.FullName;
        throw new ConfigurationException("Extract the complete ZIP and keep the root .env file.");
    }

    public static void Load(string file)
    {
        foreach (string raw in File.ReadLines(file))
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
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}

public sealed class Arguments
{
    readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> flags = new(StringComparer.OrdinalIgnoreCase);
    public Arguments(string[] args)
    {
        for (int index = 0; index < args.Length; index++)
        {
            string item = args[index];
            if (item is "--demo" or "--second-azure") { flags.Add(item); continue; }
            if (item is not ("--provider" or "--model" or "--prompt"))
                throw new ConfigurationException($"Unknown argument: {item}");
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--"))
                throw new ConfigurationException($"Supply a value after {item}.");
            values[item] = args[++index];
        }
    }
    public bool Has(string name) => flags.Contains(name) || values.ContainsKey(name);
    public string? Get(string name) => values.GetValueOrDefault(name);
}

public sealed record Connection(string Provider, string Model, Uri Endpoint, bool Demo);

public sealed class CourseContext : IDisposable
{
    readonly Dictionary<Connection, IChatClient> clients = [];
    public string Root { get; }
    public Arguments Args { get; }
    public bool Demo => Args.Has("--demo");
    public CourseContext(string[] args)
    {
        Root = DotEnv.FindRoot();
        DotEnv.Load(Path.Combine(Root, ".env"));
        Args = new Arguments(args);
        if (Demo) Console.WriteLine("DEMO MODE: synthetic answers and usage; no model inference.");
    }
    public string? Setting(string name) => Environment.GetEnvironmentVariable(name)?.Trim() is { Length: > 0 } s ? s : null;
    public string Required(string name) => Setting(name) is { } value && !value.Contains('<') && !value.Contains('>')
        ? value : throw new ConfigurationException($"Set {name} in the shared root .env file.");
    public string Model(string name, string demoValue) => Setting(name) ?? (Demo ? demoValue : Required(name));
    public int Number(string name, int fallback, int min, int max)
    {
        string? raw = Setting(name);
        if (raw is null) return fallback;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < min || n > max)
            throw new ConfigurationException($"{name} must be between {min} and {max}.");
        return n;
    }
    public Connection Connect(string? provider = null, string? model = null)
    {
        provider ??= Args.Get("--provider") ?? Setting("MAF_MODEL_PROVIDER") ?? "foundry-local";
        provider = provider.ToLowerInvariant();
        if (provider is not ("azure" or "foundry" or "foundry-local"))
            throw new ConfigurationException("Provider must be azure, foundry or foundry-local.");
        string modelKey = provider switch { "azure" => "AZURE_OPENAI_DEPLOYMENT_NAME", "foundry" => "FOUNDRY_MODEL_A", _ => "FOUNDRY_LOCAL_MODEL" };
        model ??= Args.Get("--model")
            ?? (provider == "foundry-local" ? Setting("FOUNDRY_LOCAL_API_MODEL") : null)
            ?? Model(modelKey, $"demo-{provider}");
        if (string.IsNullOrWhiteSpace(model)) throw new ConfigurationException($"Set {modelKey}.");
        if (Demo) return new(provider, model, new Uri("https://demo.invalid/"), true);
        string endpointKey = provider switch { "azure" => "AZURE_OPENAI_ENDPOINT", "foundry" => "FOUNDRY_MODELS_BASE_URL", _ => "FOUNDRY_LOCAL_BASE_URL" };
        return new(provider, model, ValidateEndpoint(provider, Required(endpointKey)), false);
    }
    public static Uri ValidateEndpoint(string provider, string value)
    {
        if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http") || uri.UserInfo.Length > 0
            || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new ConfigurationException("Use an absolute HTTP(S) endpoint without credentials, query or fragment.");
        if (provider == "azure" && (uri.Scheme != "https" || uri.AbsolutePath != "/"))
            throw new ConfigurationException("Azure requires its HTTPS resource root.");
        if (provider == "foundry" && (uri.Scheme != "https" || uri.AbsolutePath != "/openai/v1/"))
            throw new ConfigurationException("Foundry requires a supported HTTPS /openai/v1/ endpoint.");
        if (provider == "foundry-local" && (!uri.IsLoopback || uri.AbsolutePath != "/v1/"))
            throw new ConfigurationException("Foundry Local requires a loopback /v1/ URL.");
        return uri;
    }
    public IChatClient Client(Connection connection)
    {
        if (!clients.TryGetValue(connection, out var client))
        {
            client = ClientFactory.Create(this, connection);
            clients.Add(connection, client);
        }
        return client;
    }
    public void Save(string name, object value) => Json.Save(Path.Combine(Root, "output", name), value);
    public void Dispose() { foreach (var client in clients.Values) client.Dispose(); }
}

public static class CourseApp
{
    public static async Task<int> RunAsync(string[] args, Func<CourseContext, Task> action)
    {
        try { using var context = new CourseContext(args); await action(context); return 0; }
        catch (Exception ex)
        {
            Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
                status = "error", errorType = ex.GetType().Name,
                message = ex is ConfigurationException ? ex.Message : "Check configuration, model access and the connection."
            }));
            return 1;
        }
    }
}
