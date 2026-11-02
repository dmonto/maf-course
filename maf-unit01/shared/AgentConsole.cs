using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace MafCourse.Unit01.Shared;

/// <summary>Common Azure connection, input validation, cancellation and console output.</summary>
public static class AgentConsole
{
    public static async Task<int> RunAsync(
        string[] args,
        string instructions,
        AIFunction tool,
        string agentName = "ServiceDeskIntro")
    {
        try
        {
            PackageEnvironment.Load();
            string endpoint = PackageEnvironment.RequiredSetting("AZURE_OPENAI_ENDPOINT");
            string deployment = PackageEnvironment.RequiredSetting("AZURE_OPENAI_DEPLOYMENT_NAME");
            string question = args.Length == 0
                ? "What is the status of the payroll service?"
                : string.Join(" ", args);

            if (question.Length > 2000)
                throw new ArgumentException("The question must contain at most 2,000 characters.");

            var client = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential());
            AIAgent agent = client.GetChatClient(deployment).AsAIAgent(
                name: agentName,
                instructions: instructions,
                tools: [tool]);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            Console.WriteLine($"User: {question}");
            var response = await agent.RunAsync(question, cancellationToken: timeout.Token);
            Console.WriteLine($"Agent: {response}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("The run exceeded the 45-second time budget.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine(ex.Message);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Run failed ({ex.GetType().Name}).");
            Console.Error.WriteLine("Check your root .env, Azure CLI login, deployment and access.");
        }
        return 1;
    }
}
