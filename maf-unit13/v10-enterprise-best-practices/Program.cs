using Unit13;
using System.Text.Json;
using System.Security.Claims;

return await Entry.RunAsync("v10", args, async context =>
{
    int suiteExit = await AgentSuite.RunAsync(context);
    if (suiteExit != 0) return suiteExit;
    int gateExit = await PythonGate.RunAsync(context.UnitRoot, context.Artifacts);
    context.Save("release-evidence.json", new { mode = context.Options.Mode, policyVersion = Lab.PolicyVersion,
        policyAndAuditAccepted = gateExit == 0, modelInferenceExecuted = context.Options.Mode == "azure",
        hostedAuthenticationVerified = false, productionAuthorization = "requires hosted integration tests",
        cloudGovernanceVerified = false, cloudSideEffects = false });
    return gateExit;
});
