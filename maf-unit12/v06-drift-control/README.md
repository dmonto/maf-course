# Drift control

Independent entry point for GitBook Unit 12. Stable code is referenced from `Shared/Unit12.Common`; configuration comes from the single unit-root `.env`.

From the unit root after building the solution:

```powershell
.\Scripts\Run-Example.ps1 -Example v06 -Mode fixture -NoBuild
```

Expected: The default diagnostic pair flags a VPN regression with unchanged evidence; exit 1.

Provide --baseline and --candidate to inspect retained reports. Signals support investigation; no statistical drift detector is implemented.

You can also run this project directly:

```powershell
dotnet run --project .\v06-drift-control\Unit12.V06.csproj -- fixture
```

Reports are stored under `artifacts/v06`. The one shared environment and complete setup instructions are in the root README.
