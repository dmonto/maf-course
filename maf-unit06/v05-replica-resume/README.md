# v05: Use of distributed storage

Simulate replica A and B as separate processes using one local SQLite store.

The executable entry point is `Program.cs`; stable code lives in `../Shared/Unit06.Common`. Build `Maf.Unit06.slnx` from the package root first.

From this directory:

```powershell
& ../Scripts/Run-Example.ps1 -Example v05 -Demo -NoBuild
```

See the package README for the root `.env`, live Azure settings, expected output and follow-up commands.
