# Automated response evaluation

Independent entry point for GitBook Unit 12. Stable code is referenced from `Shared/Unit12.Common`; configuration comes from the single unit-root `.env`.

From the unit root after building the solution:

```powershell
.\Scripts\Run-Example.ps1 -Example v01 -Mode fixture -NoBuild
```

Expected: Four independent service-desk cases run. Each has a new tool recorder. All fixture checks pass; exit 0.

Review dataset identities and the saved actual tool evidence.

To run fresh MAF and judge inference, configure Azure and use `-Mode azure`.

You can also run this project directly:

```powershell
dotnet run --project .\v01-automated-response-evaluation\Unit12.V01.csproj -- fixture
```

Reports are stored under `artifacts/v01`. The one shared environment and complete setup instructions are in the root README.
