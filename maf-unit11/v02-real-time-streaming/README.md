# Real-time processing

Run from the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v02 -Demo
```

Expected: Provisional text fragments, first-text latency and one final explanation.

Use -Live for the Azure MAF stream after loading the common .env and logging in.

Shared code: `../Shared/Unit11.Common`. Edit only `../.env`; keep this directory inside the complete package. See the root README for installation, direct dotnet commands and mode details.
