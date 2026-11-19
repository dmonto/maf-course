# External data validation

Reject malformed, stale, unsupported or cross-scope external evidence.

Run from the maf-unit10 root:

```powershell
./Scripts/Run-Example.ps1 -Example v06 -Demo
```

Expected: Every invalid fixture case is rejected. The notification excludes external free-text instructions.

The significant implementation is in `Shared/Contracts.cs, Shared/Connectors.cs and Shared/DemoHttp.cs`. `Program.cs` is this project's independent entry point. The one common `.env` and shared source directories remain at the unit root.
