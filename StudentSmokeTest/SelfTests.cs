using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;

namespace StudentSmokeTest;

internal static class SelfTests
{
    private static readonly Dictionary<string, string> Sample = new()
    {
        ["TENANT_ID"] = "11111111-1111-1111-1111-111111111111",
        ["SUBSCRIPTION_ID"] = "22222222-2222-2222-2222-222222222222",
        ["RESOURCE_GROUP"] = "rg-course",
        ["AZURE_AI_PROJECT_ENDPOINT"] = "https://course.services.ai.azure.com/api/projects/course-project",
        ["AZURE_OPENAI_ENDPOINT"] = "https://course.openai.azure.com/",
        ["AZURE_OPENAI_DEPLOYMENT_1"] = "course-chat",
        ["AZURE_OPENAI_EMBEDDINGS_DEPLOYMENT"] = "course-embeddings",
        ["AZURE_AI_SEARCH_ENDPOINT"] = "https://course.search.windows.net/",
        ["AZURE_AI_SEARCH_INDEX_NAME"] = "student-01",
        ["AZURE_STORAGE_ACCOUNT"] = "coursestorage",
        ["AZURE_STORAGE_CONTAINER"] = "course-lab"
    };

    private static Config TestConfig(bool writes = false, Dictionary<string, string>? overrides = null)
    {
        var values = new Dictionary<string, string>(Sample) { ["SMOKE_WRITE_TESTS"] = writes ? "1" : "0" };
        if (overrides is not null) foreach (var pair in overrides) values[pair.Key] = pair.Value;
        return Config.Load(name => values.GetValueOrDefault(name));
    }

    public static async Task<int> RunAsync()
    {
        int passed = 0, failed = 0;
        async Task Test(string name, Func<Task> action)
        {
            try { await action(); passed++; Console.WriteLine($"[PASS] {name}"); }
            catch (Exception ex) { failed++; Console.WriteLine($"[FAIL] {name}: {ex.GetType().Name}"); }
        }
        Console.WriteLine("Offline self-tests: fake tokens and intercepted HTTP only; no Azure access or charges.");

        await Test("Core success includes the actual MAF SDK tool round trip", async () =>
        {
            using var fake = new FakeService();
            using var http = new HttpClient(fake);
            var tokens = new FakeCredential();
            var runner = new Runner(quiet: true);
            await new Checks(TestConfig(), tokens, http).ExecuteAsync(runner);
            Assert(runner.Passed && runner.Results.Count(r => r.Status == "PASS") == 7);
            Assert(runner.Results.Count(r => r.Status == "SKIP") == 2);
            Assert(fake.ChatRequests == 2 && fake.SawToolResult && fake.ResponsesStoreDisabled && fake.SawDocumentQuery);
            Assert(tokens.Scopes.Contains(Checks.AiScope) && tokens.Scopes.Contains(Checks.SearchScope) &&
                tokens.Scopes.Contains(Checks.StorageScope) && tokens.Scopes.Contains(Checks.ArmScope));
            Assert(fake.SawBearer && fake.BlobCount == 0 && fake.IndexCount == 0);
        });
        await Test("Legacy local-model settings cannot block core readiness", async () =>
        {
            using var fake = new FakeService();
            using var http = new HttpClient(fake);
            var runner = new Runner(quiet: true);
            var config = TestConfig(overrides: new()
            {
                ["OLLAMA_HOST"] = "not-a-valid-endpoint",
                ["OLLAMA_MODEL"] = "<unavailable-model>"
            });
            await new Checks(config, new FakeCredential(), http).ExecuteAsync(runner);
            Assert(runner.Passed && runner.Results.Count == 9);
        });
        await Test("Optional writes verify content and clean both temporary resources", async () =>
        {
            using var fake = new FakeService();
            using var http = new HttpClient(fake);
            var runner = new Runner(quiet: true);
            await new Checks(TestConfig(writes: true), new FakeCredential(), http).ExecuteAsync(runner);
            Assert(runner.Passed && runner.Results.Count(r => r.Status == "PASS") == 9);
            Assert(fake.BlobDeletes == 1 && fake.IndexDeletes == 1 && fake.BlobCount == 0 && fake.IndexCount == 0);
        });
        await Test("A Search data-plane 403 prevents a READY result", async () =>
        {
            using var fake = new FakeService { SearchDenied = true };
            using var http = new HttpClient(fake);
            var runner = new Runner(quiet: true);
            await new Checks(TestConfig(), new FakeCredential(), http).ExecuteAsync(runner);
            Assert(!runner.Passed && runner.Results.Single(r => r.Name == "Search document query").Status == "FAIL");
        });
        await Test("Text without a function invocation fails the MAF test", async () =>
        {
            using var fake = new FakeService { OmitToolCall = true };
            using var http = new HttpClient(fake);
            var runner = new Runner(quiet: true);
            await runner.RunAsync("MAF", new Checks(TestConfig(), new FakeCredential(), http).MafAsync);
            Assert(!runner.Passed && runner.Results[0].Detail.Contains("without invoking"));
        });
        await Test("Blob is deleted even when its read-back fails", async () =>
        {
            using var fake = new FakeService { BlobDownloadFails = true };
            using var http = new HttpClient(fake);
            var runner = new Runner(quiet: true);
            await runner.RunAsync("Blob", new Checks(TestConfig(writes: true), new FakeCredential(), http).BlobWriteAsync);
            Assert(!runner.Passed && fake.BlobDeletes == 1 && fake.BlobCount == 0);
        });
        await Test("Search per-document rejection fails and still deletes the index", async () =>
        {
            using var fake = new FakeService { RejectSearchDocument = true };
            using var http = new HttpClient(fake);
            var runner = new Runner(quiet: true);
            await runner.RunAsync("Search", new Checks(TestConfig(writes: true), new FakeCredential(), http).SearchWriteAsync);
            Assert(!runner.Passed && fake.IndexDeletes == 1 && fake.IndexCount == 0);
        });
        await Test("Cleanup failure is reported with the temporary resource name", async () =>
        {
            using var fake = new FakeService { BlobDeleteDenied = true };
            using var http = new HttpClient(fake);
            var runner = new Runner(quiet: true);
            await runner.RunAsync("Blob", new Checks(TestConfig(writes: true), new FakeCredential(), http).BlobWriteAsync);
            Assert(!runner.Passed && runner.Results[0].Detail.Contains("cleanup failed") && fake.BlobCount == 1);
        });
        await Test("Timeout prevents a READY result", async () =>
        {
            var runner = new Runner(quiet: true);
            await runner.RunAsync("Timeout", _ => Task.FromException<string>(new OperationCanceledException()));
            Assert(!runner.Passed && runner.Results[0].Detail.Contains("Timed out"));
        });
        await Test("Invalid tenant, endpoint and write flag are rejected before network access", () =>
        {
            RejectConfig("TENANT_ID", "not-a-guid");
            RejectConfig("AZURE_OPENAI_ENDPOINT", "https://example.com/");
            RejectConfig("AZURE_AI_PROJECT_ENDPOINT", "https://course.services.ai.azure.com/");
            RejectConfig("SMOKE_WRITE_TESTS", "yes");
            RejectConfig("AZURE_AI_SEARCH_INDEX_NAME", "<assigned-index>");
            Assert(TestConfig(overrides: new() { ["AZURE_OPENAI_ENDPOINT"] = "https://course.openai.azure.com/openai/v1/" }).OpenAiBase.AbsolutePath == "/openai/v1/");
            return Task.CompletedTask;
        });
        await Test("HTTP diagnostics are actionable and raw secret-bearing errors are suppressed", () =>
        {
            foreach (int status in new[] { 401, 403, 404, 429 }) Assert(Runner.Explain(new ServiceException(status)).Contains(status.ToString()));
            Assert(!Runner.Explain(new InvalidOperationException("token=DO_NOT_PRINT")).Contains("DO_NOT_PRINT"));
            return Task.CompletedTask;
        });
        Console.WriteLine($"SELF-TEST RESULT: {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static void RejectConfig(string key, string value)
    {
        try { TestConfig(overrides: new() { [key] = value }); }
        catch (CheckException) { return; }
        throw new InvalidOperationException("Invalid configuration was accepted.");
    }

    private static void Assert(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Self-test assertion failed.");
    }

    private sealed class FakeCredential : TokenCredential
    {
        public HashSet<string> Scopes { get; } = [];
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct)
        {
            foreach (var scope in context.Scopes) Scopes.Add(scope);
            return new AccessToken("offline-fake-token", DateTimeOffset.UtcNow.AddHours(1));
        }
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct) => new(GetToken(context, ct));
    }

    private sealed class FakeService : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> blobs = [];
        private readonly HashSet<string> indexes = [];
        public bool SearchDenied { get; init; }
        public bool OmitToolCall { get; init; }
        public bool BlobDownloadFails { get; init; }
        public bool BlobDeleteDenied { get; init; }
        public bool RejectSearchDocument { get; init; }
        public int ChatRequests { get; private set; }
        public bool SawToolResult { get; private set; }
        public bool ResponsesStoreDisabled { get; private set; }
        public bool SawDocumentQuery { get; private set; }
        public bool SawBearer { get; private set; }
        public int BlobDeletes { get; private set; }
        public int IndexDeletes { get; private set; }
        public int BlobCount => blobs.Count;
        public int IndexCount => indexes.Count;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            var path = uri.AbsolutePath;
            SawBearer |= request.Headers.Authorization?.Parameter == "offline-fake-token";
            if (uri.Host == "management.azure.com") return Json(new { id = $"/subscriptions/{Sample["SUBSCRIPTION_ID"]}/resourceGroups/rg-course" });
            if (uri.Host == "course.services.ai.azure.com" && path.EndsWith("/agents")) return Json(new { @object = "list", data = Array.Empty<object>() });
            if (uri.Host == "course.openai.azure.com")
            {
                using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                if (path.EndsWith("/chat/completions"))
                {
                    ChatRequests++;
                    SawToolResult |= doc.RootElement.GetProperty("messages").EnumerateArray().Any(m =>
                        m.GetProperty("role").GetString() == "tool" && m.GetProperty("content").ToString().Contains("MAF_SMOKE_OK"));
                    if (ChatRequests == 1 && !OmitToolCall)
                        return Json(new { id = "chatcmpl-smoke", @object = "chat.completion", created = 1700000000, model = "course-chat",
                            choices = new[] { new { index = 0, finish_reason = "tool_calls", message = new { role = "assistant", content = (string?)null,
                                tool_calls = new[] { new { id = "call-smoke", type = "function", function = new { name = "GetCourseStatus", arguments = "{}" } } } } } },
                            usage = new { prompt_tokens = 10, completion_tokens = 10, total_tokens = 20 } });
                    return Json(new { id = "chatcmpl-smoke-2", @object = "chat.completion", created = 1700000001, model = "course-chat",
                        choices = new[] { new { index = 0, finish_reason = "stop", message = new { role = "assistant", content = "MAF_SMOKE_OK" } } },
                        usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 } });
                }
                if (path.EndsWith("/responses"))
                {
                    ResponsesStoreDisabled = doc.RootElement.GetProperty("store").ValueKind == JsonValueKind.False;
                    return Json(new { output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = "OK" } } } } });
                }
                if (path.EndsWith("/embeddings")) return Json(new { data = new[] { new { embedding = new[] { 0.1, 0.2, 0.3 } } } });
            }
            if (uri.Host == "course.search.windows.net")
            {
                if (path == "/indexes" && request.Method == HttpMethod.Post)
                {
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                    Assert(indexes.Add(body.RootElement.GetProperty("name").GetString()!));
                    return Json(new { ok = true }, HttpStatusCode.Created);
                }
                var index = path.Split('/')[2];
                if (request.Method == HttpMethod.Delete)
                {
                    Assert(index.StartsWith("maf-smoke-") && indexes.Remove(index));
                    IndexDeletes++;
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
                if (path.EndsWith("/docs/index"))
                    return Json(new { value = new[] { new { key = "smoke-1", status = !RejectSearchDocument, statusCode = RejectSearchDocument ? 403 : 201 } } });
                if (path.EndsWith("/docs/search"))
                {
                    if (index == "student-01")
                    {
                        SawDocumentQuery = true;
                        if (SearchDenied) return new HttpResponseMessage(HttpStatusCode.Forbidden);
                        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                        Assert(body.RootElement.GetProperty("top").GetInt32() == 0);
                        return Json(new { value = Array.Empty<object>() });
                    }
                    Assert(indexes.Contains(index));
                    return Json(new { value = new[] { new { id = "smoke-1" } } });
                }
            }
            if (uri.Host == "coursestorage.blob.core.windows.net")
            {
                if (uri.Query.Contains("comp=list"))
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<EnumerationResults><Blobs /></EnumerationResults>", Encoding.UTF8, "application/xml") };
                Assert(path.StartsWith("/course-lab/smoke/"));
                if (request.Method == HttpMethod.Put)
                {
                    Assert(request.Headers.GetValues("If-None-Match").Single() == "*");
                    Assert(!blobs.ContainsKey(path));
                    blobs[path] = await request.Content!.ReadAsByteArrayAsync(ct);
                    return new HttpResponseMessage(HttpStatusCode.Created);
                }
                if (request.Method == HttpMethod.Get)
                {
                    if (BlobDownloadFails) return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(blobs[path]) };
                }
                if (request.Method == HttpMethod.Delete)
                {
                    if (BlobDeleteDenied) return new HttpResponseMessage(HttpStatusCode.Forbidden);
                    Assert(blobs.Remove(path));
                    BlobDeletes++;
                    return new HttpResponseMessage(HttpStatusCode.Accepted);
                }
            }
            // A missing fixture must fail; the handler never forwards a request to the network.
            throw new InvalidDataException("Unexpected intercepted request.");
        }

        private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }
}
