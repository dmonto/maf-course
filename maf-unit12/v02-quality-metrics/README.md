# Quality metrics

Independent entry point for GitBook Unit 12. Stable code is referenced from `Shared/Unit12.Common`; configuration comes from the single unit-root `.env`.

From the unit root after building the solution:

```powershell
.\Scripts\Run-Example.ps1 -Example v02 -Mode fixture -NoBuild
```

Expected: Synthetic validator inputs demonstrate missing, nonfinite and out-of-range scores. An unsupported recovery claim illustrates the limits of exact fields. Fixture scenarios pass; exit 0.

The numeric validator inputs are test data, not measured model scores. Azure mode evaluates the four standard cases with real judges.

To run fresh MAF and judge inference, configure Azure and use `-Mode azure`.

You can also run this project directly:

```powershell
dotnet run --project .\v02-quality-metrics\Unit12.V02.csproj -- fixture
```

Reports are stored under `artifacts/v02`. The one shared environment and complete setup instructions are in the root README.
