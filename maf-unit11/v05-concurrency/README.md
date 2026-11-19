# Concurrency management

Run from the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v05 -Demo
```

Expected: Eight completed independent events and maximumConcurrentAttempts=2 with the default .env.

Use -Count 8 -DelayMs 150 to change the batch and fixture delay.

Shared code: `../Shared/Unit11.Common`. Edit only `../.env`; keep this directory inside the complete package. See the root README for installation, direct dotnet commands and mode details.
