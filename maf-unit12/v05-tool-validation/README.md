# Tool validation

Independent entry point for GitBook Unit 12. Stable code is referenced from `Shared/Unit12.Common`; configuration comes from the single unit-root `.env`.

From the unit root after building the solution:

```powershell
.\Scripts\Run-Example.ps1 -Example v05 -Mode fixture -NoBuild
```

Expected: Actual function names, arguments, results and checks are printed. The default fixture passes; exit 0.

Run with fixture-bad to see a supported tool call followed by a contradicted answer: toolTrace passes, status fails.

To run fresh MAF and judge inference, configure Azure and use `-Mode azure`.

You can also run this project directly:

```powershell
dotnet run --project .\v05-tool-validation\Unit12.V05.csproj -- fixture
```

Reports are stored under `artifacts/v05`. The one shared environment and complete setup instructions are in the root README.
