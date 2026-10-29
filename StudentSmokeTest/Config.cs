using System.Text.RegularExpressions;

namespace StudentSmokeTest;

internal sealed record Config(
    string TenantId, string SubscriptionId, string ResourceGroup,
    Uri ProjectEndpoint, string ProjectApiVersion,
    Uri OpenAiBase, string ChatDeployment, string ResponsesDeployment, string EmbeddingsDeployment,
    Uri SearchEndpoint, string SearchIndex, string StorageAccount, string StorageContainer,
    bool WriteTests, Uri? OllamaEndpoint, string? OllamaModel)
{
    public static Config Load(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        string? Optional(params string[] names) => names.Select(read).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
        string Required(params string[] names)
        {
            var value = Optional(names);
            if (string.IsNullOrWhiteSpace(value) || value.Contains('<') || value.Contains('>') ||
                value.Contains("REPLACE", StringComparison.OrdinalIgnoreCase))
                throw new CheckException($"Set {names[0]} to the value assigned by the coordinator.");
            return value;
        }
        string GuidValue(string name)
        {
            var value = Required(name);
            if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
                throw new CheckException($"{name} must be a non-empty GUID.");
            return id.ToString();
        }
        Uri AzureUri(string name, params string[] suffixes)
        {
            if (!Uri.TryCreate(Required(name), UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                !uri.IsDefaultPort || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
                !suffixes.Any(s => uri.Host.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
                throw new CheckException($"{name} must be the assigned Azure HTTPS endpoint, without credentials or query parameters.");
            return uri;
        }
        var project = AzureUri("AZURE_AI_PROJECT_ENDPOINT", ".services.ai.azure.com");
        if (!Regex.IsMatch(project.AbsolutePath, @"^/api/projects/[^/]+/?$"))
            throw new CheckException("AZURE_AI_PROJECT_ENDPOINT must end with /api/projects/<project>.");
        var openAi = AzureUri("AZURE_OPENAI_ENDPOINT", ".openai.azure.com", ".services.ai.azure.com", ".cognitiveservices.azure.com");
        if (openAi.AbsolutePath is not ("/" or "/openai/v1" or "/openai/v1/"))
            throw new CheckException("AZURE_OPENAI_ENDPOINT must be the resource root or its /openai/v1/ endpoint.");
        openAi = new Uri(openAi.GetLeftPart(UriPartial.Authority) + "/openai/v1/");
        var search = AzureUri("AZURE_AI_SEARCH_ENDPOINT", ".search.windows.net");
        if (search.AbsolutePath != "/") throw new CheckException("AZURE_AI_SEARCH_ENDPOINT must be the service root.");
        var account = Required("AZURE_STORAGE_ACCOUNT", "AZURE_STORAGE_ACCOUNT_NAME");
        if (!Regex.IsMatch(account, "^[a-z0-9]{3,24}$")) throw new CheckException("AZURE_STORAGE_ACCOUNT must contain 3-24 lowercase letters or digits.");
        var container = Required("AZURE_STORAGE_CONTAINER", "AZURE_STORAGE_CONTAINER_NAME");
        if (!Regex.IsMatch(container, "^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$") || container.Contains("--"))
            throw new CheckException("AZURE_STORAGE_CONTAINER must be a valid lowercase container name.");
        var index = Required("AZURE_AI_SEARCH_INDEX_NAME");
        if (!Regex.IsMatch(index, "^[a-z0-9][a-z0-9_-]{0,126}[a-z0-9]$"))
            throw new CheckException("AZURE_AI_SEARCH_INDEX_NAME must be the assigned lowercase index name.");
        var write = Optional("SMOKE_WRITE_TESTS") ?? "0";
        if (write is not ("0" or "1")) throw new CheckException("SMOKE_WRITE_TESTS must be 0 or 1.");
        var projectVersion = Optional("AZURE_AI_PROJECT_API_VERSION") ?? "v1";
        if (!Regex.IsMatch(projectVersion, @"^(v1|\d{4}-\d{2}-\d{2}(-preview)?)$"))
            throw new CheckException("AZURE_AI_PROJECT_API_VERSION must be v1 or the API version assigned by the coordinator.");
        Uri? ollama = null;
        if (Optional("OLLAMA_HOST") is { } host)
        {
            if (!Uri.TryCreate(host.TrimEnd('/') + "/", UriKind.Absolute, out ollama) ||
                ollama.Scheme is not ("http" or "https") || ollama.UserInfo.Length > 0 ||
                ollama.Query.Length > 0 || ollama.Fragment.Length > 0 || ollama.AbsolutePath != "/")
                throw new CheckException("OLLAMA_HOST must be an HTTP(S) service root, for example http://localhost:11434.");
        }
        var model = Optional("OLLAMA_MODEL");
        if (model is not null && (model.Contains('<') || model.Contains('>')))
            throw new CheckException("Replace the OLLAMA_MODEL placeholder, or leave it empty.");
        if (model is not null && ollama is null) throw new CheckException("Set OLLAMA_HOST when setting OLLAMA_MODEL.");
        var chat = Required("AZURE_OPENAI_DEPLOYMENT_1", "AZURE_OPENAI_CHAT_FAST_DEPLOYMENT", "AZURE_OPENAI_DEPLOYMENT_NAME");
        return new Config(GuidValue("TENANT_ID"), GuidValue("SUBSCRIPTION_ID"), Required("RESOURCE_GROUP"),
            project, projectVersion, openAi, chat, Optional("AZURE_OPENAI_RESPONSES_DEPLOYMENT") ?? chat,
            Required("AZURE_OPENAI_EMBEDDINGS_DEPLOYMENT", "AZURE_OPENAI_EMBEDDING_DEPLOYMENT"),
            search, index, account, container, write == "1", ollama, model);
    }
}

internal sealed class CheckException(string message) : Exception(message);
