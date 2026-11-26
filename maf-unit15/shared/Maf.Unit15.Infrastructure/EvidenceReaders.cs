using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Maf.Unit15.Application;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace Maf.Unit15.Infrastructure;

public sealed class FixtureEvidenceReader : IEvidenceReader
{
    public Task<Evidence> ReadAsync(string service, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LabConsole.Write("FIXTURE", $"Return canned evidence for '{service}'; no network call.");
        return Task.FromResult(new Evidence(service, service switch
            { "vpn" => "degraded", "email" => "healthy", _ => throw new ArgumentException("Unknown service.") }, "fixture"));
    }
}

public sealed class HttpEvidenceReader : IEvidenceReader, IDisposable
{
    private readonly HttpClient client;
    public HttpEvidenceReader(string baseUrl)
    {
        if (baseUrl is not ("http://127.0.0.1:8080" or "http://127.0.0.1:8082"))
            throw new ArgumentException("Use the local evidence host on port 8080 or 8082.");
        client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(25) };
    }
    public async Task<Evidence> ReadAsync(string service, CancellationToken ct)
    {
        using var span = Unit15Diagnostics.Source.StartActivity("evidence-http");
        LabConsole.Write("HTTP REQUEST", $"POST {client.BaseAddress}api/status");
        LabConsole.Write("HTTP JSON BODY", JsonSerializer.Serialize(new { service }));
        var watch = Stopwatch.StartNew();
        using var response = await client.PostAsJsonAsync("/api/status", new { service }, ct);
        LabConsole.Write("HTTP RESPONSE", $"{(int)response.StatusCode} in {watch.ElapsedMilliseconds} ms");
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        byte[] bytes = new byte[16385];
        int length = 0, read;
        while (length < bytes.Length && (read = await input.ReadAsync(bytes.AsMemory(length), ct)) != 0) length += read;
        if (length > 16384) throw new InvalidDataException("Evidence exceeds the response limit.");
        var evidence = JsonSerializer.Deserialize<Evidence>(bytes.AsSpan(0, length), EventContract.Json)
            ?? throw new InvalidDataException("Empty evidence response.");
        if (evidence.Service != service || evidence.Mode is not ("fixture" or "azure") ||
            evidence.Status is not ("healthy" or "degraded")) throw new InvalidDataException("Evidence contract rejected.");
        LabConsole.Write("CHECK", $"PASS | HTTP evidence contract accepted (service={evidence.Service}, status={evidence.Status}, mode={evidence.Mode})");
        return evidence;
    }
    public void Dispose() => client.Dispose();
}

public sealed record ToolEvidence(string Service, string Status, string Summary);
public sealed class StatusTools(string selectedService)
{
    public int Calls { get; private set; }
    public static ToolEvidence Catalog(string service) => service switch
    {
        "vpn" => new(service, "degraded", "Synthetic evidence: VPN connectivity is degraded."),
        "email" => new(service, "healthy", "Synthetic evidence: email is healthy."),
        _ => throw new ArgumentException("Unknown synthetic service.")
    };
    [Description("Read synthetic status for the request-bound service. No write actions.")]
    public ToolEvidence GetServiceStatus([Description("The requested service: vpn or email.")] string service)
    {
        if (service != selectedService) throw new InvalidOperationException("Service scope mismatch.");
        Calls++;
        return Catalog(service);
    }
}

public sealed class MafEvidenceReader : IEvidenceReader
{
    private readonly ChatClient chat;
    public MafEvidenceReader()
    {
        string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value && !value.Contains('<')
            ? value : throw new InvalidOperationException($"Load the assigned {key}.");
        var endpoint = new Uri(Required("AZURE_OPENAI_ENDPOINT"));
        if (endpoint.Scheme != "https" || !endpoint.IsDefaultPort || endpoint.AbsolutePath != "/" ||
            endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            !endpoint.Host.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use the assigned HTTPS Azure OpenAI resource-root endpoint.");
        string identity = Environment.GetEnvironmentVariable("Runner__Identity") ?? "cli";
        TokenCredential credential = identity switch
        {
            "cli" => new AzureCliCredential(),
            "managed" => new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),
            "workload" => new WorkloadIdentityCredential(),
            _ => throw new InvalidOperationException("Runner__Identity must be cli, managed or workload.")
        };
        string deployment = Required("AZURE_OPENAI_DEPLOYMENT_NAME");
        LabConsole.Write("AZURE", $"new AzureOpenAIClient({endpoint}, {credential.GetType().Name}).GetChatClient(deployment=\"{deployment}\"); identity={identity}");
        chat = new AzureOpenAIClient(endpoint, credential).GetChatClient(deployment);
    }
    public async Task<Evidence> ReadAsync(string service, CancellationToken ct)
    {
        var tools = new StatusTools(service);
        LabConsole.Write("MAF", "chat.AsAIAgent(...); tool=AIFunctionFactory.Create(GetServiceStatus); MaximumIterationsPerRequest=3; OpenTelemetry on");
        AIAgent agent = chat.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "unit15-status-evidence",
            UseProvidedChatClientAsIs = true,
            ChatOptions = new ChatOptions
            {
                Instructions = """
                    Call GetServiceStatus for the requested service.
                    Return only JSON with service, status and summary.
                    Copy all three values exactly from the tool result.
                    Never claim a repair, notification or another write action.
                    """,
                MaxOutputTokens = 250,
                ResponseFormat = ChatResponseFormat.Json,
                Tools = [AIFunctionFactory.Create(tools.GetServiceStatus)]
            }
        }, clientFactory: inner => new FunctionInvokingChatClient(inner)
        {
            MaximumIterationsPerRequest = 3,
            AllowConcurrentInvocation = false,
            IncludeDetailedErrors = false
        }).AsBuilder().UseOpenTelemetry(sourceName: "Maf.Unit15.Agent",
            configure: options => options.EnableSensitiveData = false).Build();
        LabConsole.Write("MAF", $"await agent.RunAsync(\"Requested service: {service}\") -> the model decides to call the tool");
        AgentResponse response = await agent.RunAsync($"Requested service: {service}", cancellationToken: ct);
        LabConsole.Write("MAF RESULT", response.Text);
        LabConsole.Write("TOOL", $"GetServiceStatus was called {tools.Calls} time(s).");
        var answer = JsonSerializer.Deserialize<ToolEvidence>(response.Text, EventContract.Json);
        if (tools.Calls == 0 || answer != StatusTools.Catalog(service))
            throw new InvalidDataException("Agent evidence contract failed.");
        return new Evidence(answer.Service, answer.Status, "azure");
    }
}
