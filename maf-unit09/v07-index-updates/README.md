# v07: Index updates

Run from `maf-unit09`:

```powershell
.\Scripts\Run-Example.ps1 -Example v07 -Demo
.\Scripts\Run-Example.ps1 -Example v07 -Live
```

Stages and uploads v2, queries v1 and v2 independently, and leaves the serving version unchanged. Live mode needs index-write access.

All code comments and output are in English. Common code, fixtures, prompts and `.env` are resolved from the unit root. See the root README for credentials, required roles, optional arguments and live write behavior. Read the final `RESULT` JSON line.
