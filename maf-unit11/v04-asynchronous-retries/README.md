# Asynchronous scenarios

Run from the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v04 -Demo
```

Expected: Retry event: Completed on attempt 2. Poison event: Dead on attempt 1. No failed-attempt text is committed.

This demonstration explicitly injects local fixture faults.

Shared code: `../Shared/Unit11.Common`. Edit only `../.env`; keep this directory inside the complete package. See the root README for installation, direct dotnet commands and mode details.
