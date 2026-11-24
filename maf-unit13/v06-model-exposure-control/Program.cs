using Unit13;
using System.Text.Json;
using System.Security.Claims;

return await Entry.RunAsync("v06", args, context =>
{
    context.FixtureOnly();
    string[] hosts = ["lab-openai.openai.azure.com"];
    string accepted = "https://lab-openai.openai.azure.com/";
    EndpointPolicy.Validate(accepted, hosts);
    string[] rejected =
    [
        "http://lab-openai.openai.azure.com/", "https://unapproved.openai.azure.com/",
        "https://lab-openai.openai.azure.com.attacker.invalid/", "https://user@lab-openai.openai.azure.com/",
        "https://lab-openai.openai.azure.com/openai/v1/", "https://lab-openai.openai.azure.com/?key=sample",
        "https://lab-openai.openai.azure.com/#fragment", "https://lab-openai.openai.azure.com:8443/"
    ];
    foreach (string value in rejected)
    {
        bool blocked = false;
        try { EndpointPolicy.Validate(value, hosts); } catch (ArgumentException) { blocked = true; }
        Lab.Require(blocked, "Unapproved destination accepted.");
    }
    context.Save("endpoint-policy.json", new { acceptedHost = hosts[0], rejectedCount = rejected.Length,
        maximumToolIterations = 3, requestBudgetSeconds = 120,
        scope = "Validates local configuration; does not provision private endpoints or configure Azure networking." });
    Lab.Show(new { approved = 1, rejected = rejected.Length });
    return Task.FromResult(0);
});
