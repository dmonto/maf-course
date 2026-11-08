# v05: Secure document access

Run from `maf-unit09`:

```powershell
.\Scripts\Run-Example.ps1 -Example v05 -Demo
.\Scripts\Run-Example.ps1 -Example v05 -Live
```

Employee results exclude payroll; tenant A cannot see BRAVO-CANARY; tenant B can; an empty permission set returns zero evidence.

All code comments and output are in English. Common code, fixtures, prompts and `.env` are resolved from the unit root. See the root README for credentials, required roles, optional arguments and live write behavior. Read the final `RESULT` JSON line.
