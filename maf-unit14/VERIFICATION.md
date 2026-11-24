# Validation record

Initially validated on 8 November 2026 on Linux using .NET SDK 10.0.100, ASP.NET Core runtime 10.0.0, PowerShell 7.4.6 and Python. The Podman update was checked again on 21 November 2026 with the same SDK and PowerShell versions.

- All 15 projects restored with their pinned dependencies and committed package lock files. A subsequent locked restore passed.
- The full solution compiled with zero warnings and zero errors.
- All ten section entry projects passed in fixture mode.
- The real native HTTP host passed the original Python contract probe. Health, release, mode, prompt version, synthetic tool evidence and instance identity were checked.
- The portability gate rejected the wrong expected release. The recovery rehearsal rejected a wrong VPN HTTP status, restored an actual local configuration snapshot and accepted the previous release's contract.
- Additional .NET checks passed for unknown services, bounded request admission, HTTP 429 with Retry-After, rejection of fixture evidence labeled as Azure evidence, HPA ownership of replica count, invalid model endpoints, literal environment loading and process-variable precedence.
- All PowerShell scripts parsed successfully.
- Isolated PowerShell command simulations checked App Service identity/port arguments and health JSON configuration; complete AKS workload-identity/HPA rendering; release capture; full pod-template recovery; whitelisted App Service settings recovery; and registry image-digest argument handling.
- The deployment simulations replaced Azure CLI, kubectl and the container CLI with controlled local functions in a child process. They executed no cloud, Kubernetes or container operations.
- The API published successfully in Release configuration with `UseAppHost=false`, matching the container build's publish step. The published DLL ran outside the source tree with environment-only configuration and passed a PowerShell-invoked probe whose output path contained spaces.
- Project/solution XML, pipeline YAML and Python syntax were checked. `Python/smoke.py` matches its retained original GitBook listing exactly.

The live Azure-backed MAF path compiled but was not invoked against an assigned Azure deployment. Podman is not available in the preparation environment, so the actual multi-stage image build/container execution and Windows VM initialization were not performed here. App Service and AKS deployment, federated credentials, registry/model roles, ingress, Metrics Server, autoscaling and hosted recovery require the assigned environment and the corresponding functional verification.

The small fixture catalog is synthetic in both execution modes. The local checks do not establish real caller authentication, corporate-data authorization, production isolation, model quality or load capacity. The pipeline template retains the distinction between portability, behavioral evaluation, security evidence and hosted Azure verification.
