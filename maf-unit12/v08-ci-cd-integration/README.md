# Integration into CI/CD

Independent entry point for GitBook Unit 12. Stable code is referenced from `Shared/Unit12.Common`; configuration comes from the single unit-root `.env`.

From the unit root after building the solution:

```powershell
.\Scripts\Run-Example.ps1 -Example v08 -Mode fixture -NoBuild
```

Expected: The local CI demonstration verifies a passing fixture and a rejected bad fixture; exit 0.

Scripts/Test-FixtureGate.ps1 additionally validates actual child-process exit codes. Copy the workflow template to the repository root to enable it.

You can also run this project directly:

```powershell
dotnet run --project .\v08-ci-cd-integration\Unit12.V08.csproj -- fixture
```

Reports are stored under `artifacts/v08`. The one shared environment and complete setup instructions are in the root README.
