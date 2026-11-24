# Result analysis

Independent entry point for GitBook Unit 12. Stable code is referenced from `Shared/Unit12.Common`; configuration comes from the single unit-root `.env`.

From the unit root after building the solution:

```powershell
.\Scripts\Run-Example.ps1 -Example v09 -Mode fixture -NoBuild
```

Expected: The default demonstration inspects a deliberately failing fixture and writes an HTML evidence report; analysis completes with exit 0.

The candidate failure stays visible in the report. Use --report to inspect an existing run; New-QualityReport.ps1 generates separate aieval HTML after Azure evaluation.

You can also run this project directly:

```powershell
dotnet run --project .\v09-result-analysis\Unit12.V09.csproj -- fixture
```

Reports are stored under `artifacts/v09`. The one shared environment and complete setup instructions are in the root README.
