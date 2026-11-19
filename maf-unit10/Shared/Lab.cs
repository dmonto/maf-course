using System.Text.Json;
using System.Text.RegularExpressions;

internal static class Output
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    public static void Result(object value) => Console.WriteLine("RESULT " + JsonSerializer.Serialize(value, Json));
}

internal sealed class Options
{
    private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    private static readonly string[] Flags = ["--demo", "--live", "--send", "--approve", "--agent"];
    private static readonly string[] Settings = ["--profile", "--service", "--case", "--operation"];
    public Options(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (values.ContainsKey(key)) throw new ArgumentException("Duplicate option.");
            if (Flags.Contains(key)) values.Add(key, "true");
            else if (Settings.Contains(key) && i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                values.Add(key, args[++i]);
            else throw new ArgumentException($"Unknown or incomplete option: {key}.");
        }
        if (Has("--demo") && Has("--live")) throw new ArgumentException("Choose --demo or --live.");
        if (Has("--approve") && Has("--live"))
            throw new ArgumentException("Automatic approval is available only for the local demo.");
    }
    public bool Has(string key) => values.ContainsKey(key);
    public string Get(string key, string fallback) => values.GetValueOrDefault(key, fallback);
    public bool Demo => !Has("--live");
}

internal static class UnitFiles
{
    public static string Root()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "manifest.json")) &&
                File.Exists(Path.Combine(directory.FullName, "Unit10.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Keep the project inside the maf-unit10 directory.");
    }
    public static void LoadEnv(string root)
    {
        string path = Path.Combine(root, ".env");
        if (!File.Exists(path)) return;
        foreach (string original in File.ReadAllLines(path))
        {
            string line = original.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int split = line.IndexOf('=');
            if (split < 1) throw new FormatException("Invalid .env assignment.");
            string key = line[..split].Trim(), value = line[(split + 1)..].Trim();
            if (!Regex.IsMatch(key, "^[A-Z][A-Z0-9_]*$")) throw new FormatException("Invalid .env variable name.");
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];
            // An explicit nonempty process setting takes precedence over the common file.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)) && value.Length > 0)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}

internal sealed class LabContext : IDisposable
{
    public string Root { get; }
    public string Id { get; }
    public Options Options { get; }
    public bool Demo => Options.Demo;
    public Caller Caller { get; }
    public string Service { get; }
    public string DatabaseFile { get; }
    public TicketStore Tickets { get; }
    public DemoHttp? Fixture { get; }
    public HttpClient Http { get; }
    public Uri ApiRoot { get; }
    public string ApiToken { get; }
    public IntegrationTools Tools { get; }
    public string Channel => Demo ? "C000TRAINING" : LabConfig.Channel();
    public Uri SlackEndpoint => !Demo && LabConfig.LiveSlack ? new("https://slack.com/api/chat.postMessage") :
        new("http://127.0.0.1:5098/slack/chat.postMessage");
    public string SlackToken => Demo ? DemoHttp.Token : LabConfig.Required(LabConfig.LiveSlack ? "SLACK_BOT_TOKEN" : "CORPORATE_API_TOKEN");

    public LabContext(string root, string id, Options options, string? dataset = null)
    {
        Root = root; Id = id; Options = options;
        Caller = Caller.ForProfile(options.Get("--profile", id == "ex01" ? "employee" : "operator"));
        Service = options.Get("--service", "vpn");
        string directory = Path.Combine(root, "data", id, dataset ?? Caller.Subject);
        Directory.CreateDirectory(directory);
        DatabaseFile = Path.Combine(directory, "unit10.db");
        Tickets = new TicketStore(DatabaseFile);
        Tickets.Initialize();
        ApiRoot = Demo ? new("http://127.0.0.1:5098/") : LabConfig.StatusRoot();
        ApiToken = Demo ? DemoHttp.Token : LabConfig.Required("CORPORATE_API_TOKEN");
        HttpMessageHandler handler;
        if (Demo) { Fixture = new DemoHttp(); handler = Fixture; }
        else handler = new HttpClientHandler { AllowAutoRedirect = false };
        Http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        Tools = new IntegrationTools(Caller, Tickets, Http, ApiRoot, ApiToken);
    }
    public Task<Notification> PrepareAsync(CancellationToken ct) => Tools.PrepareAsync(Service, Channel, ct);
    public SlackSender Sender() => new(Http, SlackEndpoint, SlackToken);
    public async Task<bool> ReviewAsync(Notification item, string operationId)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { destination = Demo ? "LOCAL in-memory fixture" :
            (LabConfig.LiveSlack ? "LIVE training Slack" : "LOCAL HTTP fixture"), operationId, item }, Output.Json));
        if (!Options.Has("--send")) return false;
        if (Demo && Options.Has("--approve"))
        {
            Console.WriteLine("Local demo approval selected; no external message is sent.");
            return true;
        }
        Console.WriteLine($"Type SEND {operationId} to approve this exact channel and text:");
        return await Console.In.ReadLineAsync() == $"SEND {operationId}";
    }
    public void Dispose() => Http.Dispose();
}

internal static class Lab
{
    public static async Task<int> RunAsync(string[] args, string id, Func<LabContext, Task<object>> scenario)
    {
        try
        {
            string root = UnitFiles.Root();
            UnitFiles.LoadEnv(root);
            if (args is ["fixture"])
            {
                await FixtureServer.RunAsync();
                return 0;
            }
            var options = new Options(args);
            using var context = new LabContext(root, id, options);
            object result = await scenario(context);
            Output.Result(new { example = id, mode = context.Demo ? "local-demo" : "configured-live",
                result });
            return 0;
        }
        catch (Exception ex)
        {
            Output.Result(new { status = "error", errorType = ex.GetType().Name, message = ex.Message });
            return 1;
        }
    }
}
