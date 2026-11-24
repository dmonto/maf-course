# Comparative model evaluation

Independent entry point for GitBook Unit 12. Stable code is referenced from `Shared/Unit12.Common`; configuration comes from the single unit-root `.env`.

From the unit root after building the solution:

```powershell
.\Scripts\Run-Example.ps1 -Example v04 -Mode fixture -NoBuild
```

Expected: The default controlled fixture pair rejects the known bad candidate; exit 1. Azure mode compares two assigned deployments.

Configure baseline, candidate and judge in the one shared .env. Pairing rejects changed prompts, fixtures, judges or sample coverage.

You can also run this project directly:

```powershell
dotnet run --project .\v04-comparative-model-evaluation\Unit12.V04.csproj -- fixture
```

Reports are stored under `artifacts/v04`. The one shared environment and complete setup instructions are in the root README.
