# Integration contract verification

Verify the integration boundaries beyond individual happy-path examples.

Run from the maf-unit10 root:

```powershell
./Scripts/Run-Example.ps1 -Example verify -Demo
```

Expected: All checks pass, including schema hiding, real function invocation, SQL isolation, expiry, uncertain replay and concurrent claims.

The significant implementation is in `Program.cs`. `Program.cs` is this project's independent entry point. The one common `.env` and shared source directories remain at the unit root.
