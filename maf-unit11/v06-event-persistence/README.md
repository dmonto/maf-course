# Event persistence

Run from the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v06 -Demo
```

Expected: Three independent child processes seed, process and verify the same saved envelope and SQLite database.

The final RESULT records their process IDs, the exact envelope file and the durable job.

Shared code: `../Shared/Unit11.Common`. Edit only `../.env`; keep this directory inside the complete package. See the root README for installation, direct dotnet commands and mode details.
