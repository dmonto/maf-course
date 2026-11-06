# v09: Integration with corporate storage

Run from `maf-unit09`:

```powershell
.\Scripts\Run-Example.ps1 -Example v09 -Demo
.\Scripts\Run-Example.ps1 -Example v09 -Live
```

Downloads the manifest and four Markdown files from unit09/ in the assigned container, then validates and chunks them. Demo copies local files.

All code comments and output are in English. Common code, fixtures, prompts and `.env` are resolved from the unit root. See the root README for credentials, required roles, optional arguments and live write behavior. Read the final `RESULT` JSON line.
