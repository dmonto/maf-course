# Testing automation

Independent entry point for GitBook Unit 12. Stable code is referenced from `Shared/Unit12.Common`; configuration comes from the single unit-root `.env`.

From the unit root after building the solution:

```powershell
.\Scripts\Run-Example.ps1 -Example v07 -Mode fixture -NoBuild
```

Expected: The full runner writes retained JSON evidence. fixture returns 0, fixture-bad returns 1; Azure errors or invalid judgments return 2.

In Azure mode, each fresh MAF sample is followed by separate relevance and grounding evaluation. Agent time excludes judge time.

To run fresh MAF and judge inference, configure Azure and use `-Mode azure`.

You can also run this project directly:

```powershell
dotnet run --project .\v07-testing-automation\Unit12.V07.csproj -- fixture
```

Reports are stored under `artifacts/v07`. The one shared environment and complete setup instructions are in the root README.
