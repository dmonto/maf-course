# Multi-agent scalability

Observe multiple simultaneous cases and the effect of a required specialist timeout.

## Run from this directory

```powershell
dotnet run --project ./Unit08.V08.csproj -- combined --demo
if ($LASTEXITCODE -notin @(0, 2)) { throw 'The example failed.' }
```

The runtime locates the single `.env` at the `maf-unit08` root. No local copy is needed. To use the PowerShell runner, run this from the unit root:

```powershell
& ./Scripts/Run-Example.ps1 -Example v08 -Demo -NoBuild
```

## Expected result

scalability-observed for successful cases; needs-review and no brief for required failed work. The limiter is per case, so the host peak can exceed two.

Application events go to standard error. One JSON result goes to standard output, with `Example`, `Mode`, `Stage` and `Data`. Exit 0 means the demonstration completed; exit 2 means required work needs review; exit 1 means invalid input, configuration, cancellation or an invalid result.

## Live mode

After editing the shared `.env` and running `Scripts/Login-Azure.ps1`, replace `--demo` with `--live`, or use the root runner with `-Live`. MAF calls use Azure CLI identity and the assigned deployment. There is no fallback from a failed live call to deterministic advice.

Stable code is in [Shared/Unit08.Common](../Shared/Unit08.Common). See the [unit README](../README.md) for switches, examples and limitations.
