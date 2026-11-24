# Local verification

After building the solution, run from the unit root:

```powershell
dotnet run --no-build --project .\Verification\Unit12.Checks.csproj
```

Expected: `43 verification checks passed`, exit 0. Generated test runs live under `artifacts/verification`. Artificial metric values are explicitly test data. The MAF wiring check uses the real framework with a scripted local transport and records actual function invocation; it does not call Azure or measure model quality.

The broader process-level entry point is `Scripts/Test-All.ps1`. See `VALIDATION.md` for completed checks and limits.
