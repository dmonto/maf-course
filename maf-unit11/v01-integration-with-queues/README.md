# Integration with queues

Run from the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v01 -Demo
```

Expected: One durable row, duplicate suppression, payload conflict and one committed claim.



Shared code: `../Shared/Unit11.Common`. Edit only `../.env`; keep this directory inside the complete package. See the root README for installation, direct dotnet commands and mode details.
