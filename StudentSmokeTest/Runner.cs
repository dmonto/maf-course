using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.Identity;
using System.ClientModel;

namespace StudentSmokeTest;

internal sealed record CheckResult(string Name, string Status, string Detail, long DurationMs);

internal sealed class Runner(bool quiet = false)
{
    public List<CheckResult> Results { get; } = [];
    public bool Passed => Results.All(r => r.Status != "FAIL");

    public async Task RunAsync(string name, Func<CancellationToken, Task<string>> action)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            Add(new CheckResult(name, "PASS", await action(timeout.Token), timer.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            Add(new CheckResult(name, "FAIL", Explain(ex), timer.ElapsedMilliseconds));
        }
    }

    public void Skip(string name, string detail) => Add(new CheckResult(name, "SKIP", detail, 0));

    private void Add(CheckResult result)
    {
        Results.Add(result);
        if (!quiet) Console.WriteLine($"[{result.Status}] {result.Name}: {result.Detail}");
    }

    public async Task SaveAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"smoke-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        var report = new { utc = DateTimeOffset.UtcNow, readyForCoreLabs = Passed, checks = Results };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Report: {Path.GetFullPath(path)}");
    }

    public static string Explain(Exception ex)
    {
        if (ex is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
            return Explain(aggregate.InnerExceptions[0]);
        if (ex is CheckException) return ex.Message;
        int? status = ex switch
        {
            ServiceException service => service.Status,
            ClientResultException client => client.Status,
            RequestFailedException azure => azure.Status,
            HttpRequestException http when http.StatusCode.HasValue => (int)http.StatusCode.Value,
            _ => null
        };
        if (status.HasValue)
            return status.Value switch
            {
                400 => "HTTP 400: check deployment capabilities, API version and Search schema with the coordinator.",
                401 => "HTTP 401: run az login --tenant $env:TENANT_ID again; check the tenant and token audience.",
                403 => "HTTP 403: ask the administrator to check your data-plane role, its scope and the service firewall; allow time for role propagation.",
                404 => "HTTP 404: check the endpoint, project, deployment, index or container name with the coordinator.",
                409 or 412 => $"HTTP {status}: temporary-resource collision or precondition failure; rerun the test.",
                429 => "HTTP 429: quota/rate limit reached; wait briefly and ask the coordinator to check capacity.",
                >= 500 => $"HTTP {status}: service unavailable; retry and contact the coordinator if it continues.",
                _ => $"HTTP {status}: ask the coordinator to review the service configuration."
            };
        if (ex is AuthenticationFailedException or CredentialUnavailableException)
            return "Azure CLI authentication failed. Run az login --tenant $env:TENANT_ID and az account set --subscription $env:SUBSCRIPTION_ID; check MFA and Conditional Access.";
        if (ex is OperationCanceledException or TimeoutException)
            return "Timed out after up to 60 seconds. Check connectivity, proxy, firewall and model capacity, then retry.";
        if (ex is HttpRequestException)
            return "Network/TLS error. Check DNS, HTTPS proxy, certificates and outbound firewall rules.";
        if (ex is JsonException or InvalidDataException)
            return "Unexpected service response. Check the endpoint/API version; a proxy may be replacing the response.";
        // Never print raw SDK errors, response bodies, prompts, tokens or credentials.
        return $"{ex.GetType().Name}: ask the coordinator to reproduce this check and review its configuration.";
    }
}

internal sealed class ServiceException(int status) : Exception
{
    public int Status { get; } = status;
}
