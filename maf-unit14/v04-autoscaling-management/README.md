# v04: Autoscaling management

A bounded HPA and quota calculation distinguish the scaling layers.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v04 -Mode fixture -NoBuild
```

Expected: `RESULT` has `passed=true` and the exact new artifact directory. Stable implementation is in `Shared/Unit14.Common`. This project uses the single unit-root `.env`.

Expected arithmetic: two replicas at 80% average CPU versus a 60% target produce three desired replicas; 250m CPU requests make the 60% target approximately 150m. A 60 RPM model budget with three model calls per run permits at most 20 runs/minute on that request budget. These are illustrative calculations.

`Deploy-Aks.ps1 -WithHpa` removes the fixed replica count before applying its HPA. Metrics Server and cluster/node scaling are separate prerequisites. An I/O-bound model call may keep CPU low despite rising latency.
