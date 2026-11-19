using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.Text.Json;

internal static class AgentHost
{
    public static AIFunction[] ReadTools(IntegrationTools tools) =>
        [AIFunctionFactory.Create(tools.GetLiveStatusAsync), AIFunctionFactory.Create(tools.GetOpenTicketCount)];

    public static async Task<object> InspectAsync(LabContext context)
    {
        context.Caller.Demand(context.Service);
        var tools = ReadTools(context.Tools);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        if (context.Demo)
        {
            // Invoke real function wrappers without pretending that a model selected the calls.
            var arguments = new AIFunctionArguments { ["service"] = context.Service };
            object? status = await tools[0].InvokeAsync(arguments, budget.Token);
            object? count = await tools[1].InvokeAsync(arguments, budget.Token);
            return new { modelInvoked = false, invocation = "explicit-function-tools",
                status, openTickets = count, registeredTools = tools.Select(t => t.Name) };
        }
        var endpoint = new Uri(LabConfig.Required("AZURE_OPENAI_ENDPOINT"));
        if (endpoint.Scheme != "https" || !endpoint.Host.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase) ||
            endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0)
            throw new InvalidOperationException("Use the assigned Azure OpenAI resource-root endpoint.");
        var client = new AzureOpenAIClient(endpoint, new AzureCliCredential());
        AIAgent agent = client.GetChatClient(LabConfig.Required("AZURE_OPENAI_DEPLOYMENT_NAME"))
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "corporate-integration",
                ChatOptions = new ChatOptions
                {
                    MaxOutputTokens = 350,
                    Instructions = File.ReadAllText(Path.Combine(context.Root, "Prompts", "integration.txt")),
                    Tools = tools.Cast<AITool>().ToList()
                }
            });
        var response = await agent.RunAsync($"Inspect service {context.Service}.", cancellationToken: budget.Token);
        return new { modelInvoked = true, answer = response.ToString(), registeredTools = tools.Select(t => t.Name) };
    }
}
