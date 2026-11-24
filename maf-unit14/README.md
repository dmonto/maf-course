# MAF Unit 14: executable examples

Scalability and Deployment of MAF Agents in Containers, AKS and App Service.

Source: [Unit 14 in GitBook](https://app.gitbook.com/s/5NyZDQFujwe6GeJ3CBCA/session-6/unit-14), read on 8 November 2026. `Reference/unit14-gitbook.md` and `Reference/OriginalListing` retain the source and its complete listings. The Python probe is copied verbatim. GitBook was read without modification.

## Installation and first run

Keep the complete `maf-unit14` directory under the existing `maf-course` checkout. The ZIP contains this single top-level directory: extract it into `maf-course`. Existing units have their own files and remain independent.

Requirements for local examples: .NET 10 SDK, PowerShell 7 and Python 3.10 or later. Podman is needed to build/run a container. Azure CLI and kubectl are needed only for the corresponding hosted deployment commands. Use the organization's approved PowerShell execution policy.

From the `maf-course` root:

```powershell
Set-Location .\maf-unit14
.\Scripts\Initialize-Environment.ps1
dotnet restore .\Maf.Unit14.slnx --locked-mode -m:1
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build .\Maf.Unit14.slnx --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
.\Scripts\Run-Example.ps1 -Example v01 -Mode fixture -NoBuild
.\Scripts\Test-All.ps1 -NoBuild
```

The initializer preserves an existing `.env`. The distributed `.env` has safe local defaults and empty Azure assignments; it contains no credentials. Fixture runs require no Azure connection. Package lock files are included. The first restore downloads the declared packages if they are not cached.

## Section projects

| ID | Section and directory | What runs and what to inspect |
| --- | --- | --- |
| v01 | Agent containerization — `v01-agent-containerization` | Starts a real local HTTP host and runs the Python contract probe; `host-evidence.json`, `smoke.json` |
| v02 | Deployment to App Service — `v02-app-service-deployment` | Writes a plan for the main `sitecontainers` container, managed identity and port 8080; `appservice-plan.json` |
| v03 | Architecture on AKS — `v03-aks-architecture` | Generates complete Namespace, Deployment and internal Service JSON; `kubernetes.json`, `workload-identity-plan.json` |
| v04 | Autoscaling management — `v04-autoscaling-management` | Calculates bounded replica and model-call budgets; renders `hpa.json` |
| v05 | Production monitoring — `v05-production-monitoring` | Measures real local HTTP sample latency and checks mode, release, instance and evidence contract; `smoke.json` |
| v06 | Configuration management — `v06-configuration-management` | Reports shared configuration and the three supported credential paths; `configuration.json` |
| v07 | Pipeline integration — `v07-pipeline-integration` | Accepts correct runtime evidence, then proves that a wrong expected release fails the gate; `pipeline-evidence.json` |
| v08 | Version control — `v08-version-control` | Hashes the actual shared runtime assembly and records release/prompt/config metadata; `release-evidence.json` |
| v09 | Environment management — `v09-environment-management` | Compares local, App Service, AKS and production identity contracts for the same artifact; `environment-matrix.json` |
| v10 | Rollback strategies — `v10-rollback-strategies` | Rejects a deliberately wrong fixture HTTP contract, restores an actual local configuration snapshot and probes the previous contract |

All examples finish with `RESULT` containing the exact new artifact directory. A top-level successful run returns exit code 0. Failed checks return 1. v07, v10, ex02 and the verification project deliberately invoke a failing child probe: its `FAIL` line is expected only when the enclosing project confirms the rejection and subsequently finishes successfully. No successful report is substituted for a failed run.

```powershell
.\Scripts\Run-Example.ps1 -Example v03 -NoBuild
.\Scripts\Run-Example.ps1 -Example v07 -NoBuild
.\Scripts\Run-Example.ps1 -Example v10 -NoBuild
```

## Shared environment

All entry projects locate the unit root from their executable directory. There is no per-project `.env`. The PowerShell and C# loaders accept literal assignments and optional matching quotes. They do not evaluate shell expressions or interpolate other variables. Existing nonempty process variables take precedence; use a fresh terminal or clear the relevant process variable when intentionally changing the file.

| Variable | Purpose |
| --- | --- |
| `Runner__Mode` | Default direct-host mode, `fixture` or `azure`; default `fixture` |
| `Runner__Identity` | `cli` locally, `managed` for an App Service system identity, `workload` for AKS |
| `Runner__Release` | Local expected release and Podman build label; default `14.1.0` |
| `Runner__Concurrency` | Per-process accepted work limit, 1–32; default 2 |
| `PYTHON_COMMAND` | Python executable name or full path, without arguments |
| `TENANT_ID`, `SUBSCRIPTION_ID` | Assigned Azure CLI context |
| `RESOURCE_GROUP`, `WEBAPP_NAME` | Existing assigned App Service target |
| `ACR_NAME` | Existing assigned registry used by the image-publish helper |
| `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT_NAME` | Assigned resource-root HTTPS endpoint and deployment supporting Chat Completions, JSON and function calling |
| `KUBE_CONTEXT`, `KUBE_NAMESPACE` | Existing assigned cluster context and namespace; namespace defaults to `maf-lab` |
| `WORKLOAD_CLIENT_ID` | Assigned AKS workload managed-identity client ID; federation is prepared separately |
| `APPROVED_IMAGE` | Actual retained immutable ACR reference, `registry.azurecr.io/repository@sha256:...` |
| `APPROVED_RELEASE`, `APPROVED_CONFIG_REVISION` | Reviewed recovery metadata; initial release default `14.1.0` |
| `SMOKE_BEARER_TOKEN` | Optional short-lived token accepted by the deployed entry layer |

Never replace the runtime image's release value through cloud app settings during promotion. App Service and AKS scripts set runtime mode/identity but let the image supply its build release. The SDK-backed host does not consume `AZURE_OPENAI_API_VERSION`.

The raw `Python/smoke.py` preserves the original probe and reads process variables, not `.env` directly. The supplied PowerShell wrappers and .NET projects load the shared file before invoking it. `PYTHON_COMMAND` must identify an executable, for example `python` or a full path containing spaces; a string such as `py -3` is not an executable path.

## Interactive host and local Azure-backed MAF

v01 and v05 support both modes. Without `-Serve`, they start a loopback host on an automatically selected port, execute the probe and stop. With `-Serve`, they keep a host open on loopback port 8080 until Ctrl+C; run the probe in another terminal to establish functional evidence.

```powershell
.\Scripts\Run-Example.ps1 -Example v01 -Mode fixture -Serve -NoBuild
```

In another terminal from this unit root:

```powershell
.\Scripts\Probe-Deployment.ps1 -BaseUri http://127.0.0.1:8080 -Mode fixture
```

For actual Azure model calls, complete the assigned Azure values in the shared `.env`, keep `Runner__Identity=cli` locally and run:

```powershell
.\Scripts\Login-Azure.ps1
.\Scripts\Run-Example.ps1 -Example v05 -Mode azure -NoBuild
```

The host uses `AzureOpenAIClient`, `AsAIAgent`, `AIFunctionFactory.Create`, a bounded `FunctionInvokingChatClient` and `RunAsync`. Each request gets a new request-bound tool and independent agent run. Accepted output must copy the synthetic service evidence exactly. The tool loop is bounded at three invocation iterations and each accepted request has a 20-second budget. Local fixture execution calls the read tool directly and does not test model behavior.

The fixture catalog is synthetic in both modes. This package does not implement the corporate authentication/authorization layer or business-data adapter from other units. Keep authenticated or restricted ingress and resource policies when deploying it with real dependencies. The Azure credential determines the workload's model access; it does not authenticate an end user.

## Prepare Podman on Windows

Install Podman or Podman Desktop and use PowerShell 7. Initialize the common environment as above, then:

```powershell
.\Scripts\Initialize-Podman.ps1
podman info
```

The helper reuses a working connection. If the engine is unavailable on Windows/macOS, it reuses an existing machine or initializes and starts one when needed. It does not request a second start for a running machine. An explicit `-MachineName` selects a named machine; the configured provider remains under Podman's control. Linux normally uses its local rootless engine.

## Build and test the actual container

From the unit root with Podman running:

```powershell
.\Scripts\Initialize-Podman.ps1
.\Scripts\Build-Container.ps1
.\Scripts\Test-Container.ps1
```

The test helper starts an isolated named container on an available loopback port, runs the original probe, prints the logs and removes that container. Use `-KeepContainer` to retain it for inspection. It uses the shared `Runner__Release` value, so no image tag needs to be edited manually.

The Containerfile uses a locked multi-stage build, publishes the shared API and runs as the .NET image's non-root user. It contains no `.env`, CLI login, SDK stage or package installation at startup.

To push a built candidate to the assigned registry:

```powershell
.\Scripts\Login-Azure.ps1
.\Scripts\Build-Container.ps1 -PushImage
```

The helper obtains a short-lived registry token with `az acr login --expose-token` and supplies it to `podman login --password-stdin`. The token is not passed as a command-line argument or written to artifacts. Images use the v2 image/manifest format for the existing Azure hosting targets; the `docker` format name in the build option is not an engine invocation. The local tag is fully qualified as `localhost/maf-status:<release>`.

The publish helper records the real registry digest in a new `artifacts/manual/image-*/image.json`. Review that result and load the approved reference into `APPROVED_IMAGE`. Assembly hashes produced by v08 are explicitly not OCI image digests. This build/publish helper does not supply the behavioral, security or hosted verification approvals required for production promotion.

## Existing App Service deployment

Prerequisites: an assigned sidecar-enabled Linux app, main container name `main` or an app without a main container yet, an enabled system-assigned identity, appropriate registry image-read and Azure OpenAI data-plane permissions, approved ingress and network reachability. For ACR standard RBAC, image readers use `AcrPull`; ABAC-enabled repository access uses its corresponding repository reader role. Registry and model permissions are separate.

```powershell
.\Scripts\Deploy-AppService.ps1 -Mode fixture
.\Scripts\Deploy-AppService.ps1 -Mode fixture -Apply
```

The first command prints an offline plan. `-Apply` performs the deployment. It checks the Azure tenant/subscription and the existing hosting configuration, sets the port to 8080, selects system identity for registry pulls and configures `/health/ready` through a JSON configuration file. It does not create a plan or grant roles.

Probe the HTTPS hostname printed by the deployment command. Supply `SMOKE_BEARER_TOKEN` through the shared configuration where the entry layer accepts bearer tokens; network-restricted targets require an appropriately connected runner. After verifying fixture mode, deploy with `-Mode azure -Apply` and probe with `-Mode azure`. Startup health is not functional model evidence.

## Existing AKS deployment

```powershell
.\Scripts\Deploy-Aks.ps1
.\Scripts\Deploy-Aks.ps1 -WithHpa -Apply
kubectl --context $env:KUBE_CONTEXT port-forward service/maf-status 8081:80 -n $env:KUBE_NAMESPACE
```

In another terminal, probe `http://127.0.0.1:8081`. The manifest is JSON, which kubectl accepts as a Kubernetes manifest. It includes a Namespace, Deployment and internal ClusterIP Service. The HPA option removes the fixed replica count before adding its controller. The assigned context needs permission to manage these resources, including the namespace. The scripts pass `--context` explicitly rather than changing the default context.

For Azure mode, prepare AKS OIDC/workload identity, the assigned managed identity, the exact ServiceAccount federation subject and model permissions. Load `WORKLOAD_CLIENT_ID`, then:

```powershell
.\Scripts\Deploy-Aks.ps1 -Mode azure -WithHpa -Apply
```

The generated ServiceAccount is `maf-agent` in `KUBE_NAMESPACE`. Its federation subject is `system:serviceaccount:<namespace>:maf-agent`. The pod receives `azure.workload.identity/use=true` and selects `WorkloadIdentityCredential`. The identity webhook supplies its projected token. The kubelet identity's image-read permission remains separate. The script does not create federation or role grants.

Resource-based HPA requires Metrics Server; node scaling requires configured node-pool autoscaling. Additional replicas do not increase model quota. The sample scaling arithmetic is calculated data, not observed cluster metrics. A Service port-forward connects to one selected pod and does not establish load balancing across replicas.

## Capture and restore a known release

After a real hosted functional probe passes, retain its exact report and capture the current approved deployment. Set `APPROVED_RELEASE` to that report's release. The capture helper rejects a report with a different release or mode.

```powershell
.\Scripts\Capture-Release.ps1 -Target aks -Mode azure `
    -ProbeReport 'C:\path with spaces\smoke-azure.json' `
    -ConfigRevision 'reviewed-configuration-commit' -Output .\artifacts\approved-aks.json
.\Scripts\Restore-Release.ps1 -ReleaseRecord .\artifacts\approved-aks.json
.\Scripts\Restore-Release.ps1 -ReleaseRecord .\artifacts\approved-aks.json -Apply
```

Use `-Target appservice` for the App Service snapshot. Capture requires access to the current target. Restore defaults to a plan and mutates only when `-Apply` is supplied. It rejects a target different from the assigned context/namespace or app.

For this host, the AKS record contains the complete approved pod template, including image and inline model mapping; restoration replaces that template and waits for readiness. The App Service record retains the immutable image and a whitelist of this host's nonsecret runtime settings; restoration restores those settings and the container image. No registry password, model key or bearer token is copied into the record. Protect release records as environment metadata.

Run the functional probe again with the restored release and actual mode and retain the recovery report. External ConfigMaps, secret-store versions, network grants, database changes and delivered actions have their own recovery process. The capture/restore helpers cover the image and configuration used by the supplied host, not arbitrary resources.

## Pipeline integration

`Pipelines/unit14-container.yml` is a complete candidate portability workflow. Copy it into the application repository's `.github/workflows/unit14-container.yml` when intentionally enabling it. Its working directory is `maf-unit14`; it builds the full solution, runs local checks, builds the container, probes it and retains evidence.

The workflow is a fixture portability gate. Add the existing Unit 12 evaluation and Unit 13 security gates, approved image scanning and the Azure-mode hosted verification stage before production promotion. Use federated CI identity for later registry/deployment steps and pin governed workflow actions to reviewed commit SHAs. The template is stored in this package and was not installed into GitHub during preparation.

## Troubleshooting and verification

| Symptom | Check |
| --- | --- |
| PowerShell says version requirement is unmet | Run the scripts in PowerShell 7 (`pwsh`) |
| Locked restore fails | Retain all package lock files and restore from the unit root with .NET 10 |
| Python cannot start | Set `PYTHON_COMMAND` to one Python executable, including a full path if needed |
| A child probe prints FAIL in v07/v10/ex02/checks | Check that its enclosing `RESULT` and the final suite pass; those rejections are deliberate |
| Hosted health passes but the Azure probe fails | Inspect workload identity, model permissions, endpoint/deployment, quota and response contract |
| A pod cannot pull its image | Inspect the actual digest, kubelet registry permission and registry network/DNS path |
| Plans contain `<APPROVED_IMAGE>` | Load a reviewed real image reference before applying; no digest is fabricated |
| An ACR tag was rebuilt | Select and retain the approved immutable digest; do not infer packaged bytes from a label |
| A release has healthy pods and a wrong status | Block promotion and use retained configuration/image evidence for recovery |

See `VERIFICATION.md` for the checks actually performed and their limits. `Reference/README.md` lists the packaging adaptations from the original listing.
