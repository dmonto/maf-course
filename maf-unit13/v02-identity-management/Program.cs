using Unit13;
using System.Text.Json;
using System.Security.Claims;

return await Entry.RunAsync("v02", args, context =>
{
    context.FixtureOnly();
    // These already-validated claims are a test input, not a JWT validation implementation.
    ClaimsPrincipal principal = new(new ClaimsIdentity(
        [new Claim("sub", "lab-reader"), new Claim("tid", "tenant-a"), new Claim("scp", "services.read")],
        "FixtureValidated"));
    Caller caller = IdentityMapping.FromValidatedPrincipal(principal, ["vpn"]);
    Lab.Require(caller.Subject == "lab-reader" && !caller.Services.Contains("payroll"), "Incorrect principal mapping.");
    bool unauthenticatedRejected = false;
    try { IdentityMapping.FromValidatedPrincipal(new ClaimsPrincipal(new ClaimsIdentity()), ["vpn"]); }
    catch (UnauthorizedAccessException) { unauthenticatedRejected = true; }
    Lab.Require(unauthenticatedRejected, "Unauthenticated principal accepted.");
    Decision spoof = Authorization.Evaluate(caller, "payroll", "payroll", Fixtures.Rows());
    Lab.Require(!spoof.Allowed, "A claimed role must not grant access.");
    context.Save("identity.json", new { subject = caller.Subject, tenant = caller.Tenant,
        source = "synthetic-validated-principal", unauthenticatedRejected, roleSpoofDenied = !spoof.Allowed,
        modelWorkloadCredential = "AzureCliCredential in Azure mode; separate from the synthetic caller" });
    Lab.Show(new { unauthenticatedRejected, spoofDenied = !spoof.Allowed });
    return Task.FromResult(0);
});
