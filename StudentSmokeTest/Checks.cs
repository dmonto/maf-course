using System.ClientModel.Primitives;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Azure.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace StudentSmokeTest;

internal sealed class Checks(Config config, TokenCredential credential, HttpClient http)
{
    internal const string AiScope = "https://ai.azure.com/.default";
    internal const string SearchScope = "https://search.azure.com/.default";
    internal const string StorageScope = "https://storage.azure.com/.default";
    internal const string ArmScope = "https://management.azure.com/.default";

    public async Task ExecuteAsync(Runner runner)
    {
        await runner.RunAsync("Azure resource group", ResourceGroupAsync);
        await runner.RunAsync("Foundry project / agents read", FoundryAsync);
        await runner.RunAsync("MAF agent / function tool", MafAsync);
        await runner.RunAsync("OpenAI Responses", ResponsesAsync);
        await runner.RunAsync("OpenAI embeddings", EmbeddingsAsync);
        await runner.RunAsync("Search document query", SearchAsync);
        await runner.RunAsync("Blob container read", BlobAsync);
        if (config.WriteTests)
        {
            await runner.RunAsync("Blob write / read / delete", BlobWriteAsync);
            await runner.RunAsync("Search create / upload / query / delete", SearchWriteAsync);
        }
        else
        {
            runner.Skip("Blob write / read / delete", "SMOKE_WRITE_TESTS=0; write permissions have not been verified.");
            runner.Skip("Search create / upload / query / delete", "SMOKE_WRITE_TESTS=0; index-management and write permissions have not been verified.");
        }
        if (config.OllamaEndpoint is null)
            runner.Skip("Ollama", "Optional: OLLAMA_HOST is empty.");
        else
            await runner.RunAsync("Ollama", OllamaAsync);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri uri, string? scope,
        CancellationToken ct, object? body = null, byte[]? bytes = null, bool createBlob = false)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (scope is not null)
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext([scope]), ct);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        if (scope == StorageScope)
        {
            request.Headers.Add("x-ms-version", "2023-11-03");
            request.Headers.Add("x-ms-date", DateTimeOffset.UtcNow.ToString("R"));
        }
        if (body is not null) request.Content = JsonContent.Create(body);
        if (bytes is not null) request.Content = new ByteArrayContent(bytes);
        if (createBlob)
        {
            request.Headers.Add("x-ms-blob-type", "BlockBlob");
            request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        }
        var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new ServiceException(status);
        }
        return response;
    }

    private async Task<JsonDocument> JsonAsync(HttpMethod method, Uri uri, string? scope,
        CancellationToken ct, object? body = null)
    {
        using var response = await SendAsync(method, uri, scope, ct, body);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private async Task<string> ResourceGroupAsync(CancellationToken ct)
    {
        var uri = new Uri($"https://management.azure.com/subscriptions/{config.SubscriptionId}/resourcegroups/{Uri.EscapeDataString(config.ResourceGroup)}?api-version=2021-04-01");
        using var doc = await JsonAsync(HttpMethod.Get, uri, ArmScope, ct);
        if (!doc.RootElement.TryGetProperty("id", out var id) ||
            !id.GetString()!.Contains($"/subscriptions/{config.SubscriptionId}/", StringComparison.OrdinalIgnoreCase))
            throw new CheckException("Resource-group response does not match SUBSCRIPTION_ID.");
        return "Assigned resource group is readable in the configured subscription.";
    }

    private async Task<string> FoundryAsync(CancellationToken ct)
    {
        var uri = new Uri(config.ProjectEndpoint.AbsoluteUri.TrimEnd('/') + "/agents?api-version=" + Uri.EscapeDataString(config.ProjectApiVersion));
        using var doc = await JsonAsync(HttpMethod.Get, uri, AiScope, ct);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException();
        return "Project endpoint accepted Entra authentication and the agents read request.";
    }

    internal async Task<string> MafAsync(CancellationToken ct)
    {
        var toolCalls = 0;
        string GetCourseStatus()
        {
            Interlocked.Increment(ref toolCalls);
            return "MAF_SMOKE_OK";
        }
        var options = new OpenAIClientOptions
        {
            Endpoint = config.OpenAiBase,
            Transport = new HttpClientPipelineTransport(http),
            RetryPolicy = new ClientRetryPolicy(0)
        };
#pragma warning disable OPENAI001
        var client = new OpenAIClient(new BearerTokenPolicy(credential, AiScope), options);
#pragma warning restore OPENAI001
        var agent = client.GetChatClient(config.ChatDeployment).AsAIAgent(new ChatClientAgentOptions
        {
            Name = "StudentSmokeAgent",
            ChatOptions = new ChatOptions
            {
                Instructions = "Call GetCourseStatus exactly once. Then repeat its returned status. Do not call any other tool.",
                MaxOutputTokens = 128,
                Tools = [AIFunctionFactory.Create(GetCourseStatus, "GetCourseStatus", "Return the harmless course smoke-test status.")]
            }
        });
        var result = await agent.RunAsync("Check the course status using the function tool.", cancellationToken: ct);
        if (toolCalls == 0)
            throw new CheckException("Model answered without invoking the function tool. Ask the coordinator to check the tool-capable chat deployment.");
        if (string.IsNullOrWhiteSpace(result.ToString()))
            throw new CheckException("Function tool ran, but the agent produced no final text. Check the deployment/output-token budget.");
        return "MAF completed a model -> local function -> model round trip.";
    }

    private async Task<string> ResponsesAsync(CancellationToken ct)
    {
        using var doc = await JsonAsync(HttpMethod.Post, new Uri(config.OpenAiBase, "responses"), AiScope, ct,
            new { model = config.ResponsesDeployment, input = "Reply with OK.", max_output_tokens = 128, store = false });
        bool hasText = doc.RootElement.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array &&
            output.EnumerateArray().Any(item => item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array &&
                content.EnumerateArray().Any(part => part.TryGetProperty("text", out var text) && !string.IsNullOrWhiteSpace(text.GetString())));
        if (!hasText) throw new CheckException("Responses request returned no output text. Check the deployment and output-token budget.");
        return "Responses v1 returned text (store=false).";
    }

    private async Task<string> EmbeddingsAsync(CancellationToken ct)
    {
        using var doc = await JsonAsync(HttpMethod.Post, new Uri(config.OpenAiBase, "embeddings"), AiScope, ct,
            new { model = config.EmbeddingsDeployment, input = "MAF course smoke test" });
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0 ||
            !data[0].TryGetProperty("embedding", out var vector) || vector.ValueKind != JsonValueKind.Array || vector.GetArrayLength() == 0)
            throw new InvalidDataException();
        return $"Embedding generated ({vector.GetArrayLength()} dimensions).";
    }

    private Uri SearchUri(string index, string suffix = "") => new(config.SearchEndpoint,
        $"indexes/{Uri.EscapeDataString(index)}{suffix}?api-version=2024-07-01");

    private async Task<string> SearchAsync(CancellationToken ct)
    {
        using var doc = await JsonAsync(HttpMethod.Post, SearchUri(config.SearchIndex, "/docs/search"), SearchScope, ct,
            new { search = "*", top = 0, count = true });
        if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException();
        return "Document query accepted on the assigned index; metadata-only access was not used as a substitute.";
    }

    private Uri ContainerUri(string suffix = "") => new($"https://{config.StorageAccount}.blob.core.windows.net/{config.StorageContainer}{suffix}");

    private async Task<string> BlobAsync(CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, ContainerUri("?restype=container&comp=list&maxresults=1"), StorageScope, ct);
        var doc = XDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (doc.Root?.Name.LocalName != "EnumerationResults") throw new InvalidDataException();
        return "Container listing accepted; existing file names/content are not printed.";
    }

    internal async Task<string> BlobWriteAsync(CancellationToken ct)
    {
        var name = $"smoke/{Guid.NewGuid():N}.txt";
        var uri = ContainerUri("/" + name);
        var payload = Encoding.UTF8.GetBytes("MAF smoke test: temporary synthetic content.");
        bool created = false;
        Exception? failure = null;
        try
        {
            using (await SendAsync(HttpMethod.Put, uri, StorageScope, ct, bytes: payload, createBlob: true)) { }
            created = true;
            using var download = await SendAsync(HttpMethod.Get, uri, StorageScope, ct);
            if (!(await download.Content.ReadAsByteArrayAsync(ct)).SequenceEqual(payload))
                throw new CheckException("Blob download did not match the synthetic upload.");
        }
        catch (Exception ex) { failure = ex; }
        if (created)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using (await SendAsync(HttpMethod.Delete, uri, StorageScope, cleanup.Token)) { }
            }
            catch (Exception ex)
            {
                throw new CheckException($"Temporary blob cleanup failed. Ask the coordinator to remove '{name}'. {Runner.Explain(ex)}");
            }
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return "Unique temporary blob uploaded, downloaded, verified and deleted.";
    }

    internal async Task<string> SearchWriteAsync(CancellationToken ct)
    {
        var index = $"maf-smoke-{Guid.NewGuid():N}";
        bool created = false;
        Exception? failure = null;
        try
        {
            // POST collection creates only: it cannot overwrite an existing index.
            using (await SendAsync(HttpMethod.Post, new Uri(config.SearchEndpoint, "indexes?api-version=2024-07-01"), SearchScope, ct,
                new { name = index, fields = new object[] {
                    new { name = "id", type = "Edm.String", key = true, filterable = true },
                    new { name = "content", type = "Edm.String", searchable = true }
                } })) { }
            created = true;
            using var upload = await JsonAsync(HttpMethod.Post, SearchUri(index, "/docs/index"), SearchScope, ct,
                new { value = new[] { new Dictionary<string, object> { ["@search.action"] = "upload", ["id"] = "smoke-1", ["content"] = "MAF smoke test" } } });
            if (!upload.RootElement.TryGetProperty("value", out var results) || results.GetArrayLength() != 1 ||
                !results[0].TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.True)
                throw new CheckException("Search accepted the batch but rejected the synthetic document.");
            bool found = false;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                using var query = await JsonAsync(HttpMethod.Post, SearchUri(index, "/docs/search"), SearchScope, ct,
                    new { search = "*", filter = "id eq 'smoke-1'", select = "id", top = 1 });
                if (query.RootElement.TryGetProperty("value", out var docs) && docs.ValueKind == JsonValueKind.Array &&
                    docs.EnumerateArray().Any(d => d.TryGetProperty("id", out var id) && id.GetString() == "smoke-1"))
                { found = true; break; }
                if (attempt < 5) await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            if (!found) throw new CheckException("Uploaded Search document did not become queryable within the smoke-test wait period.");
        }
        catch (Exception ex) { failure = ex; }
        if (created)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using (await SendAsync(HttpMethod.Delete, SearchUri(index), SearchScope, cleanup.Token)) { }
            }
            catch (Exception ex)
            {
                throw new CheckException($"Temporary index cleanup failed. Ask the coordinator to remove '{index}'. {Runner.Explain(ex)}");
            }
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return "Unique temporary index created, document uploaded/queried, and index deleted.";
    }

    private async Task<string> OllamaAsync(CancellationToken ct)
    {
        using var tags = await JsonAsync(HttpMethod.Get, new Uri(config.OllamaEndpoint!, "api/tags"), null, ct);
        if (!tags.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException();
        if (config.OllamaModel is null) return "Server reachable. Set OLLAMA_MODEL to also verify model availability and inference.";
        var requested = config.OllamaModel.Contains(':') ? config.OllamaModel : config.OllamaModel + ":latest";
        bool exists = models.EnumerateArray().Any(m => m.TryGetProperty("name", out var name) &&
            (name.GetString() == requested || name.GetString() == config.OllamaModel));
        if (!exists) throw new CheckException("Assigned Ollama model is not downloaded. Run ollama pull $env:OLLAMA_MODEL, then retry.");
        using var inference = await JsonAsync(HttpMethod.Post, new Uri(config.OllamaEndpoint!, "api/generate"), null, ct,
            new { model = config.OllamaModel, prompt = "Reply OK.", stream = false, options = new { num_predict = 32 } });
        if (!inference.RootElement.TryGetProperty("response", out var text) || string.IsNullOrWhiteSpace(text.GetString()))
            throw new CheckException("Ollama returned no text. Check the assigned model and available memory.");
        return "Assigned model is downloaded and returned an inference response.";
    }
}
