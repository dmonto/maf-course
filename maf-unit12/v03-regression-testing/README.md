# Regression testing

Independent entry point for GitBook Unit 12. Stable code is referenced from `Shared/Unit12.Common`; configuration comes from the single unit-root `.env`.

From the unit root after building the solution:

```powershell
.\Scripts\Run-Example.ps1 -Example v03 -Mode fixture -NoBuild
```

Expected: The positive fixture returns 0 and the known bad fixture returns 1. The project confirms these expectations and itself returns 0.

The bad VPN answer fails status while the recorded tool trace passes.

You can also run this project directly:

```powershell
dotnet run --project .\v03-regression-testing\Unit12.V03.csproj -- fixture
```

Reports are stored under `artifacts/v03`. The one shared environment and complete setup instructions are in the root README.
