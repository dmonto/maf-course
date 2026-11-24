# v05: Production monitoring

Health, API results, instance identity and measured sample latency are checked.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v05 -Mode fixture -NoBuild
```

Expected: `RESULT` has `passed=true` and the exact new artifact directory. Stable implementation is in `Shared/Unit14.Common`. This project uses the single unit-root `.env`.

The original Python probe checks both health endpoints, four sequential synthetic API requests, release, mode, prompt version and instance identity. `p95Ms` is a measured sample percentile, not a load benchmark.

```powershell
.\Scripts\Login-Azure.ps1
.\Scripts\Run-Example.ps1 -Example v05 -Mode azure -NoBuild
```

This Azure mode performs real MAF calls using the configured local identity and validates actual tool evidence. Configure the common `.env` first. For a remote host use `Scripts/Probe-Deployment.ps1`, which loads the same file.
