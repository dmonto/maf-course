using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Unit14;

public static class ImagePolicy
{
    public static bool IsImmutable(string value) => Regex.IsMatch(value,
        @"^[a-z0-9][a-z0-9.-]*\.azurecr\.io/[a-z0-9][a-z0-9._/-]*@sha256:[0-9a-f]{64}$");
    public static string? Assigned()
    {
        string value = Environment.GetEnvironmentVariable("APPROVED_IMAGE") ?? "";
        if (value.Length == 0) return null;
        if (!IsImmutable(value)) throw new ArgumentException("APPROVED_IMAGE must be an immutable ACR reference.");
        return value;
    }
}
public static class Kubernetes
{
    public static string NamespaceName()
    {
        string value = Lab.Value("KUBE_NAMESPACE", "maf-lab");
        if (!Regex.IsMatch(value, "^[a-z0-9]([-a-z0-9]{0,61}[a-z0-9])?$"))
            throw new ArgumentException("Invalid Kubernetes namespace.");
        return value;
    }
    public static object Render(string image, string release, bool useHpa = false)
    {
        ApiSettings runSettings = ApiSettings.Read("fixture");
        string ns = NamespaceName();
        var labels = new Dictionary<string, string> { ["app"] = "maf-status" };
        var podSpec = new
        {
            terminationGracePeriodSeconds = 35, automountServiceAccountToken = false,
            securityContext = new { runAsNonRoot = true, seccompProfile = new { type = "RuntimeDefault" } },
            containers = new[] { new {
                name = "api", image,
                ports = new[] { new { name = "http", containerPort = 8080 } },
                env = new[] { new { name = "Runner__Mode", value = "fixture" }, new { name = "Runner__Concurrency", value = "2" },
                    new { name = "Runner__RequestTimeoutSeconds", value = runSettings.RequestTimeoutSeconds.ToString() },
                    new { name = "Runner__MaxOutputTokens", value = runSettings.MaxOutputTokens.ToString() } },
                resources = new { requests = new { cpu = "250m", memory = "256Mi" }, limits = new { cpu = "1", memory = "512Mi" } },
                securityContext = new { allowPrivilegeEscalation = false, readOnlyRootFilesystem = true,
                    capabilities = new { drop = new[] { "ALL" } } },
                volumeMounts = new[] { new { name = "temp", mountPath = "/tmp" } },
                startupProbe = new { httpGet = new { path = "/health/live", port = "http" }, periodSeconds = 2, failureThreshold = 30 },
                readinessProbe = new { httpGet = new { path = "/health/ready", port = "http" }, periodSeconds = 5 },
                livenessProbe = new { httpGet = new { path = "/health/live", port = "http" }, periodSeconds = 10 }
            } },
            volumes = new[] { new { name = "temp", emptyDir = new { sizeLimit = "64Mi" } } }
        };
        var deploymentSpec = new Dictionary<string, object?>
        {
            ["revisionHistoryLimit"] = 3, ["progressDeadlineSeconds"] = 180,
            ["strategy"] = new { type = "RollingUpdate", rollingUpdate = new { maxUnavailable = 0, maxSurge = 1 } },
            ["selector"] = new { matchLabels = labels },
            ["template"] = new { metadata = new { labels, annotations = new Dictionary<string,string> { ["maf.example/release"] = release } }, spec = podSpec }
        };
        // HPA becomes the sole owner of the replica count when requested.
        if (!useHpa) deploymentSpec["replicas"] = 2;
        var items = new List<object>
        {
            new { apiVersion = "v1", kind = "Namespace", metadata = new { name = ns } },
            new { apiVersion = "apps/v1", kind = "Deployment", metadata = new { name = "maf-status", @namespace = ns }, spec = deploymentSpec },
            new { apiVersion = "v1", kind = "Service", metadata = new { name = "maf-status", @namespace = ns },
                spec = new { type = "ClusterIP", selector = labels, ports = new[] { new { port = 80, targetPort = "http" } } } }
        };
        if (useHpa) items.Add(Hpa());
        return new { apiVersion = "v1", kind = "List", items };
    }
    public static object Hpa() => new
    {
        apiVersion = "autoscaling/v2", kind = "HorizontalPodAutoscaler",
        metadata = new { name = "maf-status", @namespace = NamespaceName() },
        spec = new { scaleTargetRef = new { apiVersion = "apps/v1", kind = "Deployment", name = "maf-status" },
            minReplicas = 2, maxReplicas = 4,
            metrics = new[] { new { type = "Resource", resource = new { name = "cpu", target = new { type = "Utilization", averageUtilization = 60 } } } },
            behavior = new { scaleDown = new { stabilizationWindowSeconds = 300 } } }
    };
}
public sealed record ScalingResult(int DesiredReplicas, int AcceptedConcurrentRuns, double TargetCpuMillicores, int MaximumRunsPerMinute);
public static class Scaling
{
    public static ScalingResult Calculate(int currentReplicas, double cpuUtilizationPercent,
        int concurrency = 2, int modelRpm = 60, int modelCallsPerRun = 3)
    {
        if (currentReplicas < 1 || cpuUtilizationPercent < 0 || concurrency < 1 || modelCallsPerRun < 1 || modelRpm < 0)
            throw new ArgumentException("Invalid scaling inputs.");
        int desired = Math.Clamp((int)Math.Ceiling(currentReplicas * cpuUtilizationPercent / 60), 2, 4);
        return new(desired, desired * concurrency, 250 * 0.60, modelRpm / modelCallsPerRun);
    }
}
public static class ReleaseEvidence
{
    public static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    public static object Record(string release) => new
    {
        release, promptVersion = Lab.PromptVersion,
        assembly = Path.GetFileName(typeof(AgentHost).Assembly.Location),
        assemblySha256 = FileHash(typeof(AgentHost).Assembly.Location), artifactKind = "dotnet-assembly",
        imageRef = ImagePolicy.Assigned(), imageVerified = false,
        modelDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME"),
        configRevision = Environment.GetEnvironmentVariable("APPROVED_CONFIG_REVISION"),
        note = "Assembly hashing does not establish an OCI image digest or approve a deployment."
    };
}
public sealed record Diagnosis(string Symptom, string Boundary, string Verification, string Promotion);
public static class BoundaryDiagnosis
{
    public static Diagnosis For(string symptom) => symptom switch
    {
        "AuthenticationFailedException" => new(symptom, "workload-to-model",
            "Check runtime identity selection, federation or managed identity and model resource permission.", "blocked: functional Azure-mode probe required"),
        "ImagePullBackOff" => new(symptom, "kubelet-to-registry",
            "Check immutable image reference, kubelet registry role and registry network/DNS path.", "blocked: pod cannot start"),
        _ => throw new ArgumentException("Unknown fixture symptom.")
    };
}
public sealed record RecoveryStep(int Order, string Action, string RequiredEvidence);
public static class Recovery
{
    public static RecoveryStep[] Plan() => [
        new(1, "Select the last approved immutable image", "Retained digest and release record"),
        new(2, "Restore compatible configuration and model mapping", "Approved configuration revision; retain current evidence"),
        new(3, "Wait for deployment readiness", "Rollout status and actual running digest"),
        new(4, "Probe the restored release in its actual mode", "Matching release, prompt and evidence contract"),
        new(5, "Retain the recovery report and promotion decision", "Report path, identity/environment metadata and gate result")
    ];
    public static async Task RehearseAsync(Context context)
    {
        string approvedConfiguration = context.Save("approved-configuration.json", new
        {
            release = "14.1.0", modelDeployment = "approved-fixture-deployment", mode = "fixture"
        });
        string activeConfiguration = context.Save("active-configuration.json", new
        {
            release = "14.2.0", modelDeployment = "candidate-changed-deployment", mode = "fixture"
        });
        LabConsole.Write("MILESTONE", "Step 1: start release 14.2.0 with an injected wrong contract; the probe must reject it.");
        ApiSettings good = ApiSettings.Read("fixture") with { Release = "14.1.0" };
        await using (var badHost = await ApiSession.StartAsync(good with { Release = "14.2.0", WrongFixtureContract = true }))
        {
            var bad = await HttpProbe.RunAsync(context, badHost.Address, "14.2.0", "fixture", "failed-release.json", requests: 2);
            LabConsole.Check("The bad release is rejected by the probe", bad.ExitCode == 1 && !bad.Report.GetProperty("passed").GetBoolean());
        }
        LabConsole.Write("MILESTONE", "Step 2: restore the approved configuration snapshot over the active one.");
        // Rehearse restoring a real local configuration file alongside the previous contract.
        File.Copy(approvedConfiguration, activeConfiguration, overwrite: true);
        using var restoredConfig = JsonDocument.Parse(File.ReadAllText(activeConfiguration));
        LabConsole.Check("The previous model mapping was restored", restoredConfig.RootElement.GetProperty("modelDeployment").GetString() == "approved-fixture-deployment");
        LabConsole.Write("MILESTONE", "Step 3: start the restored release 14.1.0 and probe it.");
        good = good with { Release = restoredConfig.RootElement.GetProperty("release").GetString()! };
        await using (var restoredHost = await ApiSession.StartAsync(good))
        {
            var restored = await HttpProbe.RunAsync(context, restoredHost.Address, "14.1.0", "fixture", "restored-release.json", requests: 2);
            LabConsole.Check("The restored release passes the probe", restored.ExitCode == 0);
        }
        context.Save("recovery-plan.json", new { mode = "fixture", steps = Plan(),
            previousModelMappingRestored = true, mappingEvidence = "Local active-configuration.json restored from its approved snapshot; no Azure configuration was changed.",
            approvedImage = ImagePolicy.Assigned(), cloudRollbackExecuted = false });
    }
}
