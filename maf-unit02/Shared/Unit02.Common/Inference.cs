using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Unit02.Common;

public static class ClientFactory
{
    public static IChatClient Create(CourseContext context, Connection connection)
    {
        if (connection.Demo) throw new InvalidOperationException("Demo mode does not create an inference client.");
        // Disable hidden SDK retries so every recorded request represents one attempt.
        if (connection.Provider == "azure")
            return new AzureOpenAIClient(connection.Endpoint, new AzureCliCredential(),
                new AzureOpenAIClientOptions { RetryPolicy = new ClientRetryPolicy(0) })
                .GetChatClient(connection.Model).AsIChatClient();
        var options = new OpenAIClientOptions { Endpoint = connection.Endpoint, RetryPolicy = new ClientRetryPolicy(0) };
        OpenAIClient sdk;
        if (connection.Provider == "foundry-local")
            sdk = new OpenAIClient(new ApiKeyCredential("notneeded"), options);
        else if (context.Setting("FOUNDRY_AUTH_MODE") == "key")
            sdk = new OpenAIClient(new ApiKeyCredential(context.Required("FOUNDRY_MODELS_API_KEY")), options);
        else
        {
            // This Entra ID constructor is marked experimental by the pinned OpenAI SDK.
#pragma warning disable OPENAI001
            sdk = new OpenAIClient(new BearerTokenPolicy(new AzureCliCredential(), "https://ai.azure.com/.default"), options);
#pragma warning restore OPENAI001
        }
        return sdk.GetChatClient(connection.Model).AsIChatClient();
    }
}

public sealed record QualityResult(bool Passed, string[] Checks);
public sealed record RunRecord(
    string RequestId, string Mode, string Provider, string EndpointHost,
    string RequestedModel, string? ReturnedModel, string? ResponseId,
    string Status, long ElapsedMs, long? InputTokens, long? OutputTokens,
    long? TotalTokens, bool QualityPassed, string[] QualityChecks,
    string Phase, int RetryCount, string? ErrorType,
    string TimingBoundary = "client-request");
public sealed record RunResult(string Text, RunRecord Record);

public static class Inference
{
    public static async Task<RunResult> RunAsync(CourseContext context, Connection connection,
        string instructions, string prompt, string demoAnswer,
        Func<string, QualityResult> assess, string phase = "single-request")
    {
        if (prompt.Length > 2000) throw new ConfigurationException("Input is limited to 2,000 characters for these examples.");
        var requestId = Guid.NewGuid().ToString("N");
        var watch = new Stopwatch();
        try
        {
            string text; string? returnedModel; string? responseId;
            long? input; long? output; long? total;
            if (context.Demo)
            {
                watch.Start();
                text = demoAnswer; returnedModel = "synthetic-demo-model"; responseId = "demo-" + requestId;
                input = 37; output = 15; total = 52;
            }
            else
            {
                var client = context.Client(connection);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(context.Number("REQUEST_TIMEOUT_SECONDS", 45, 1, 600)));
                watch.Start();
                var response = await client.GetResponseAsync(
                    [new ChatMessage(ChatRole.System, instructions), new ChatMessage(ChatRole.User, prompt)],
                    new ChatOptions { MaxOutputTokens = context.Number("MAX_OUTPUT_TOKENS", 512, 1, 8192) }, timeout.Token);
                text = response.Text;
                returnedModel = response.ModelId; responseId = response.ResponseId;
                input = response.Usage?.InputTokenCount; output = response.Usage?.OutputTokenCount;
                total = response.Usage?.TotalTokenCount;
            }
            watch.Stop();
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("The model returned no text.");
            var quality = assess(text);
            return new(text, new(requestId, context.Demo ? "demo" : "live", connection.Provider,
                connection.Endpoint.Host, connection.Model, returnedModel, responseId, "ok",
                watch.ElapsedMilliseconds, input, output, total, quality.Passed,
                quality.Checks, phase, 0, null));
        }
        catch (Exception ex)
        {
            watch.Stop();
            return new("", new(requestId, context.Demo ? "demo" : "live", connection.Provider,
                connection.Endpoint.Host, connection.Model, null, null,
                ex is OperationCanceledException ? "timeout" : "error", watch.ElapsedMilliseconds,
                null, null, null, false, ["No usable response"], phase, 0, ex.GetType().Name));
        }
    }
    public static void Print(RunResult result)
    {
        if (result.Text.Length > 0) Console.WriteLine(result.Text);
        Console.WriteLine(Json.Line(result.Record));
    }
}
