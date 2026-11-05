# v06: Consistency control

Stage two candidates at the same revision and reject the stale write.

The executable entry point is `Program.cs`; stable code lives in `../Shared/Unit06.Common`. Build `Maf.Unit06.slnx` from the package root first.

From this directory:

```powershell
& ../Scripts/Run-Example.ps1 -Example v06 -Demo -NoBuild
```

See the package README for the root `.env`, live Azure settings, expected output and follow-up commands.
