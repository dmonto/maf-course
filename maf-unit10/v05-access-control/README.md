# Access control

Compare service-level read/write permissions and inspect real tool schemas.

Run from the maf-unit10 root:

```powershell
./Scripts/Run-Example.ps1 -Example v05 -Demo
```

Expected: Employee cannot notify; operator cannot read payroll; runtime identity and tokens are absent from the schema.

The significant implementation is in `Shared/Contracts.cs, Shared/AgentHost.cs and Program.cs`. `Program.cs` is this project's independent entry point. The one common `.env` and shared source directories remain at the unit root.
