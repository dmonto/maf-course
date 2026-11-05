# v06: Sequential orchestration

Run from the package root:

```powershell
& ./Scripts/Run-Example.ps1 -Example v06 -Demo -NoBuild
```

Build the root solution first. This project references `Shared/Unit07.Common` and discovers the single root `.env` automatically. Keep the whole package together.

See the root [README](../README.md) for this example's input variations, expected outcomes and failure checks. Runtime files belong to `data/v06/<state>` at the package root.
