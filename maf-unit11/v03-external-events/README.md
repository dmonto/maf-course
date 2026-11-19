# Reaction to external events

Run from the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v03 -Demo
```

Expected: A loopback HTTP server on port 5111. In a second terminal, run Scripts/Send-Event.ps1 -Wait.

Use -Command status to inspect persisted jobs without starting a worker.

Shared code: `../Shared/Unit11.Common`. Edit only `../.env`; keep this directory inside the complete package. See the root README for installation, direct dotnet commands and mode details.
