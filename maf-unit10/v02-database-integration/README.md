# Integration with databases

Read a local SQL aggregate through a fixed, parameterized business operation.

Run from the maf-unit10 root:

```powershell
./Scripts/Run-Example.ps1 -Example v02 -Demo
```

Expected: Training-a VPN open count 2; training-b count 1; closed tickets excluded.

The significant implementation is in `Shared/Stores.cs`. `Program.cs` is this project's independent entry point. The one common `.env` and shared source directories remain at the unit root.
