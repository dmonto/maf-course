using Unit13;
using System.Text.Json;
using System.Security.Claims;

return await Entry.RunAsync("v08", args, context =>
{
    context.FixtureOnly();
    var controls = new[]
    {
        new { id = "data-minimization", evidence = "Evidence DTO and v03 projection check", status = "demonstrated_locally" },
        new { id = "resource-access", evidence = "Authorization.Evaluate and v01 negative cases", status = "demonstrated_locally" },
        new { id = "retention", evidence = "v05 owned-file lifecycle and hold demonstration", status = "demonstrated_locally" },
        new { id = "lawful-purpose", evidence = "Approved processing purpose and responsible owner", status = "requires_organizational_review" },
        new { id = "purview-policy", evidence = "Supported integration, user context and configured policies", status = "not_configured" },
        new { id = "provider-data-policy", evidence = "Chosen API, deployment geography and monitoring arrangements", status = "requires_organizational_review" }
    };
    context.Save("control-evidence-register.json", new { intendedUse = "synthetic read-only service status",
        implementationScope = "local-lab", controls, certificationClaim = false, cloudPolicyEnforcementMeasured = false });
    Lab.Show(new { controlsMapped = controls.Length, complianceVerdict = "not_assessed", purview = "not_configured" });
    return Task.FromResult(0);
});
