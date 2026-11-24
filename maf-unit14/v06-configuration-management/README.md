# v06: Configuration management

The shared environment and Azure credential boundaries are inspected.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v06 -Mode fixture -NoBuild
```

Expected: `RESULT` has `passed=true` and the exact new artifact directory. Stable implementation is in `Shared/Unit14.Common`. This project uses the single unit-root `.env`.

`configuration.json` reports only configuration presence, mode, release, concurrency and identity responsibilities. It does not write endpoint values, bearer tokens or credential material. Existing nonempty process values override the shared `.env`.

The local Azure path is checked through v01/v05; this configuration inspection itself performs no model request. The shared host supports explicit CLI, App Service managed-identity and AKS workload-identity credentials.
