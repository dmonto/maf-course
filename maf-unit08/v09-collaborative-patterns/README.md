# Collaborative patterns

Compare sequential and concurrent consultations using the same coordinator-specialist responsibilities.

## Run from this directory

```powershell
dotnet run --project ./Unit08.V09.csproj -- combined --demo
if ($LASTEXITCODE -notin @(0, 2)) { throw 'The example failed.' }
```

The runtime locates the single `.env` at the `maf-unit08` root. No local copy is needed. To use the PowerShell runner, run this from the unit root:

```powershell
& ./Scripts/Run-Example.ps1 -Example v09 -Demo -NoBuild
```

## Expected result

patterns-compared with two timings and equivalent business stages. Exact timings and live wording can vary. Handoff, group chat and Magentic are comparison topics, not alternate engines implemented by this project.

Application events go to standard error. One JSON result goes to standard output, with `Example`, `Mode`, `Stage` and `Data`. Exit 0 means the demonstration completed; exit 2 means required work needs review; exit 1 means invalid input, configuration, cancellation or an invalid result.

## Live mode

After editing the shared `.env` and running `Scripts/Login-Azure.ps1`, replace `--demo` with `--live`, or use the root runner with `-Live`. MAF calls use Azure CLI identity and the assigned deployment. There is no fallback from a failed live call to deterministic advice.

Stable code is in [Shared/Unit08.Common](../Shared/Unit08.Common). See the [unit README](../README.md) for switches, examples and limitations.
