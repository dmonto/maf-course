# v01: Agent containerization

A real fixture HTTP host passes the Python deployment probe.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v01 -Mode fixture -NoBuild
```

Expected: `RESULT` has `passed=true` and the exact new artifact directory. Stable implementation is in `Shared/Unit14.Common`. This project uses the single unit-root `.env`.

Keep the service open for a manual probe:

```powershell
.\Scripts\Run-Example.ps1 -Example v01 -Mode fixture -Serve -NoBuild
```

The Containerfile and `Scripts/Build-Container.ps1` package `Shared/Unit14.Api`. The default section run tests a native .NET HTTP process, not a Podman container. Run the separate container commands from the root README to establish container evidence.
