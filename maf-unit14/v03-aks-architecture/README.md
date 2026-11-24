# v03: Architecture on AKS

Complete internal Kubernetes manifests are rendered as JSON.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v03 -Mode fixture -NoBuild
```

Expected: `RESULT` has `passed=true` and the exact new artifact directory. Stable implementation is in `Shared/Unit14.Common`. This project uses the single unit-root `.env`.

`kubernetes.json` contains a complete Kubernetes List with a Namespace, Deployment and ClusterIP Service. The deployment uses non-root execution, a read-only root filesystem, a temporary volume, resource requests/limits and three probes.

```powershell
.\Scripts\Deploy-Aks.ps1
.\Scripts\Deploy-Aks.ps1 -Apply
```

The plan command writes the manifest without contacting Kubernetes. Applying requires the assigned context and immutable image. Workload identity and its federation are added by `Deploy-Aks.ps1 -Mode azure`; infrastructure identity grants are prerequisites.
