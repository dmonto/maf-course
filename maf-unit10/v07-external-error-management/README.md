# External error management

Classify dependency errors and verify the different retry policies for reads and actions.

Run from the maf-unit10 root:

```powershell
./Scripts/Run-Example.ps1 -Example v07 -Demo
```

Expected: 401/403 are not retried; transient reads are bounded; every POST is attempted once; unknown outcomes stay unknown.

The significant implementation is in `Program.cs, Shared/NotificationFlow.cs and Shared/Connectors.cs`. `Program.cs` is this project's independent entry point. The one common `.env` and shared source directories remain at the unit root.
