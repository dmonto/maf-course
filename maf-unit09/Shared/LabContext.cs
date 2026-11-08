using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

internal sealed class LabContext : IDisposable
{
    public string Root { get; } = Files.FindRoot();
    public Options Options { get; }
    public bool Live { get; }
    public string ExecutionMode => Live ? "azure-live" : "local-demo";
    public LabSettings Settings { get; }
    public KnowledgeDocument[] Documents { get; private set; } = [];
    public DemoSearchService? Demo { get; }
    public CancellationToken Token => _timeout.Token;
    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromMinutes(3));
    private readonly TokenCredential _credential;
    private readonly HttpClient _http;
    private readonly AzureOpenAIClient? _openAI;
    private readonly IIndexExtension? _extension;
    public LabContext(string[] args, IIndexExtension? extension = null)
    {
        Options = new(args); Live = Options.Has("--live"); _extension = extension;
        if (Live && Options.Has("--demo")) throw new ArgumentException("Choose --demo or --live.");
        Files.LoadEnv(Path.Combine(Root, ".env"));
        Settings = Live ? LabSettings.Load() : new(new("https://demo.openai.azure.com/"), "demo-chat", "demo-embedding",
            new("https://demo.search.windows.net/"), "unit09-demo", 1536, "v1");
        if (Live)
        {
            _credential = new AzureCliCredential(); _openAI = new(Settings.OpenAIEndpoint, _credential);
            _http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
        }
        else { Demo = new(); _credential = new DemoCredential(); _http = new(Demo); }
    }
    public async Task InitializeAsync()
    {
        Documents = await Files.LoadKnowledgeAsync(Path.Combine(Root, "Knowledge"), Token);
        if (!Live) await Store().SeedAsync(Documents, Token);
    }
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        if (!Live) return DemoSearchService.Embed(text, Settings.Dimensions);
        var value = await _openAI!.GetEmbeddingClient(Settings.EmbeddingDeployment).GenerateEmbeddingAsync(text, cancellationToken: ct);
        return value.Value.ToFloats().ToArray();
    }
    public SearchStore Store(string? version = null) => new(Settings with { CatalogVersion = version ?? Settings.CatalogVersion },
        _credential, _http, EmbedAsync, _extension);
    public Task<SearchHit[]> SearchAsync(string profile, string mode, string question, string? version = null) =>
        Store(version).SearchAsync(CallerScope.ForProfile(profile), mode, question, Token);
    public async Task<object> AnswerAsync(string profile, string question, string mode = "hybrid")
    {
        var watch = Stopwatch.StartNew();
        var hits = await SearchAsync(profile, mode, question);
        long retrievalMs = watch.ElapsedMilliseconds;
        if (hits.Length == 0) return new { answer = "No supporting evidence is available to this caller.", modelInvoked = false,
            executionMode = ExecutionMode, retrievalMs, evidence = hits };
        string answer;
        if (Live)
        {
            AIAgent agent = _openAI!.GetChatClient(Settings.ChatDeployment).AsAIAgent(new ChatClientAgentOptions
            {
                Id = "support-knowledge-v1", Name = "support-knowledge", ChatOptions = new ChatOptions
                { MaxOutputTokens = 600, Instructions = await File.ReadAllTextAsync(Path.Combine(Root, "Prompts", "knowledge.txt"), Token) }
            });
            var input = new { question, evidence = hits.Select(x => new { chunkId = x.Id, x.DocumentId, x.Title, x.SourceFile, x.CatalogVersion, x.Content }) };
            var response = await agent.RunAsync(JsonSerializer.Serialize(input, Output.Json), cancellationToken: Token);
            answer = response.ToString();
        }
        else
        {
            // A deterministic evidence display exercises wiring only; no language model runs.
            string q = question.ToLowerInvariant();
            string? doc = q.Contains("vpn") || q.Contains("tunnel") ? "vpn-guide" : q.Contains("payroll") ? "payroll-guide" : q.Contains("password") ? "password-guide" : null;
            var relevant = hits.Where(h => h.DocumentId == doc).ToArray();
            answer = q.Contains("recovery") ? "The guide provides no estimated recovery time." :
                relevant.Length == 0 ? "The supplied evidence does not support an answer to this question." :
                string.Join("\n", relevant.Select(h => $"{h.Content} [{h.Id}]"));
        }
        return new { answer, modelInvoked = Live, executionMode = ExecutionMode, retrievalMs, evidence = hits };
    }
    public async Task<string> StageAsync(string version)
    {
        if (!Regex.IsMatch(version, "^[a-z0-9-]{1,20}$") || version == Settings.CatalogVersion)
            throw new ArgumentException("Choose a new catalog version.");
        string directory = Path.Combine(Root, "data", "staging-" + version);
        Directory.CreateDirectory(directory);
        foreach (string name in Documents.Select(d => d.SourceFile).Distinct())
            File.Copy(Path.Combine(Root, "Knowledge", name), Path.Combine(directory, name), true);
        var staged = Documents.Select(d => d with { CatalogVersion = version }).ToArray();
        await File.WriteAllTextAsync(Path.Combine(directory, "knowledge.json"), JsonSerializer.Serialize(staged, Output.Json), Token);
        return directory;
    }
    public async Task<string> DownloadBlobFixtureAsync()
    {
        string target = Path.Combine(Root, "data", "blob-fixture"); Directory.CreateDirectory(target);
        var files = Documents.Select(d => d.SourceFile).Prepend("knowledge.json").Distinct();
        if (!Live)
        {
            foreach (string file in files) File.Copy(Path.Combine(Root, "Knowledge", file), Path.Combine(target, file), true);
            return target;
        }
        string account = Environment.GetEnvironmentVariable("AZURE_STORAGE_ACCOUNT_NAME") ?? "";
        string container = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONTAINER_NAME") ?? "";
        if (!Regex.IsMatch(account, "^[a-z0-9]{3,24}$") || !Regex.IsMatch(container, "^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$"))
            throw new ArgumentException("Set the assigned storage account and container in the common .env.");
        var token = await _credential.GetTokenAsync(new TokenRequestContext(["https://storage.azure.com/.default"]), Token);
        foreach (string file in files)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{account}.blob.core.windows.net/{container}/unit09/{Uri.EscapeDataString(file)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Headers.Add("x-ms-version", "2023-11-03");
            request.Headers.Add("x-ms-date", DateTimeOffset.UtcNow.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            using var response = await _http.SendAsync(request, Token);
            response.EnsureSuccessStatusCode();
            await File.WriteAllBytesAsync(Path.Combine(target, file), await response.Content.ReadAsByteArrayAsync(Token), Token);
        }
        return target;
    }
    public void Dispose() { _http.Dispose(); _timeout.Dispose(); }
}
internal static class Runner
{
    public static async Task<int> RunAsync(string id, string[] args, Func<LabContext, Task> run, IIndexExtension? extension = null)
    {
        try
        {
            using var context = new LabContext(args, extension);
            Console.WriteLine($"EXAMPLE {id} MODE {context.ExecutionMode}");
            await context.InitializeAsync(); await run(context); return 0;
        }
        catch (Exception ex)
        {
            // Do not print request headers, access tokens or configuration values.
            Console.Error.WriteLine($"FAILED {ex.GetType().Name}: {ex.Message}"); return 1;
        }
    }
}
