# Secure credential management

Inspect configuration presence without exposing the values.

Run from the maf-unit10 root:

```powershell
./Scripts/Run-Example.ps1 -Example v04 -Demo
```

Expected: Each required setting has a configured Boolean; no token or endpoint value is printed.

The significant implementation is in `Shared/Lab.cs and Program.cs`. `Program.cs` is this project's independent entry point. The one common `.env` and shared source directories remain at the unit root.
