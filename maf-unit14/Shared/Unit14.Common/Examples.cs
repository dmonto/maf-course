using System.Text.Json;

namespace Unit14;

public static class Examples
{
    public static Task<int> RunAsync(string id, string[] args) => Entry.RunAsync(id, args, async context =>
    {
        if (id is not ("v01" or "v05")) context.FixtureOnly();
        switch (id)
        {
            case "v01":
            case "v05":
            {
                var settings = ApiSettings.Read(context.Mode);
                if (context.Args.Contains("--serve"))
                {
                    await using var app = AgentHost.Build(settings, "http://127.0.0.1:8080");
                    await app.RunAsync();
                    break;
                }
                await using var session = await ApiSession.StartAsync(settings);
                var result = await PythonProbe.RunAsync(context, session.Address, context.Release, context.Mode);
                Lab.Require(result.ExitCode == 0, "HTTP contract probe failed.");
                context.Save("host-evidence.json", new { mode = context.Mode, release = context.Release,
                    modelExecuted = context.Mode == "azure", report = result.Report, containerExecuted = false });
                break;
            }
            case "v02":
                context.Save("appservice-plan.json", new { planOnly = true, linuxFxVersion = "sitecontainers",
                    image = ImagePolicy.Assigned(), imageReferenceAssigned = ImagePolicy.Assigned() is not null,
                    deploymentExecuted = false,
                    mainContainer = new { name = "main", targetPort = 8080, isMain = true, authType = "SystemIdentity" },
                    runnerIdentity = "managed", healthCheckPath = "/health/ready",
                    requiredAssignments = new[] { "RESOURCE_GROUP", "WEBAPP_NAME", "APPROVED_IMAGE", "system identity registry/model roles", "authenticated or restricted ingress" },
                    script = "Scripts/Deploy-AppService.ps1" });
                break;
            case "v03":
                context.Save("kubernetes.json", Kubernetes.Render(ImagePolicy.Assigned() ?? "<APPROVED_IMAGE>", context.Release));
                context.Save("workload-identity-plan.json", new { planOnly = true,
                    serviceAccount = "maf-agent", namespaceName = Kubernetes.NamespaceName(),
                    federationSubject = $"system:serviceaccount:{Kubernetes.NamespaceName()}:maf-agent",
                    podLabel = "azure.workload.identity/use=true", runtimeIdentity = "workload",
                    registryIdentity = "kubelet identity", script = "Scripts/Deploy-Aks.ps1" });
                break;
            case "v04":
                var busy = Scaling.Calculate(2, 80);
                var waiting = Scaling.Calculate(2, 10);
                Lab.Require(busy.DesiredReplicas == 3 && busy.MaximumRunsPerMinute == 20,
                    "Scaling arithmetic mismatch.");
                Lab.Require(waiting.DesiredReplicas == 2, "Minimum bound was not retained.");
                context.Save("hpa.json", Kubernetes.Hpa());
                context.Save("scaling-observations.json", new { fixture = true, busyCpu = busy, networkWaiting = waiting,
                    note = "Calculated scenarios; no cluster metrics or model quota were measured." });
                break;
            case "v06":
                var current = ApiSettings.Read("fixture");
                context.Save("configuration.json", new { current.Mode, current.Release, current.Concurrency,
                    azureIdentity = current.Identity, source = "shared root .env plus nonempty process-variable precedence",
                    modelEndpointConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")),
                    modelDeploymentConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")),
                    supportedIdentityPaths = new[] { "cli: local process", "managed: App Service system identity", "workload: AKS projected federation" },
                    modelExecuted = false });
                break;
            case "v07":
                await using (var host = await ApiSession.StartAsync(ApiSettings.Read("fixture")))
                {
                    var good = await PythonProbe.RunAsync(context, host.Address, context.Release, "fixture", "candidate.json", requests: 2);
                    var wrongVersion = await PythonProbe.RunAsync(context, host.Address, "unexpected-release", "fixture", "rejected-release.json", requests: 1);
                    Lab.Require(good.ExitCode == 0 && wrongVersion.ExitCode == 1, "Portability gate accepted incorrect metadata.");
                    context.Save("pipeline-evidence.json", new { portabilityPassed = true, wrongReleaseRejected = true,
                        mode = "fixture", modelExecuted = false, productionPromotionApproved = false,
                        requiredFurtherGates = new[] { "Unit 12 behavioral evaluation", "Unit 13 security evidence", "image scan and digest", "Azure-mode hosted verification" } });
                }
                break;
            case "v08":
                context.Save("release-evidence.json", ReleaseEvidence.Record(context.Release));
                Lab.Require(!ImagePolicy.IsImmutable("registry.azurecr.io/maf-status:latest"), "Mutable image reference accepted.");
                break;
            case "v09":
                context.Save("environment-matrix.json", new { image = ImagePolicy.Assigned(), release = context.Release,
                    promptVersion = Lab.PromptVersion, sameBuildRequired = true,
                    environments = new[] {
                        new { name = "local", mode = "fixture", identity = "none", data = "synthetic", requiresHostedVerification = false },
                        new { name = "verification-appservice", mode = "azure", identity = "managed", data = "synthetic", requiresHostedVerification = true },
                        new { name = "verification-aks", mode = "azure", identity = "workload", data = "synthetic", requiresHostedVerification = true },
                        new { name = "production", mode = "azure", identity = "dedicated production identity", data = "separately authorized adapters", requiresHostedVerification = true }
                    }, provisionedEnvironments = false });
                break;
            case "v10":
                await Recovery.RehearseAsync(context);
                break;
            default: throw new ArgumentException("Unknown example.");
        }
    });
}
