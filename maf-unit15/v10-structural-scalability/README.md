# v10 — Structural scalability

Recover an independent audit consumer without blocking review creation.

This runnable project shares unchanged components from `../shared` and reads only the common package-root `.env`.

From the package root:

```powershell
python .\Scripts\run.py v10 --mode fixture
```

Or run this project directly:

```powershell
python .\v10-structural-scalability\main.py --mode fixture
```

## Expected result

The review subscriber commits while the audit subscription is unavailable. Audit recovery returns applied, duplicate, duplicate and creates exactly one audit record in its own database.

Every default run creates a new directory under `artifacts/v10`. Its absolute path is printed in the final JSON. The generated databases and `result.json` are retained for inspection.

The local sender hands copies to two independently persisted subscriber adapters. The canonical outbox sent flag represents the review delivery in this simulation; it is not a multi-subscriber broker checkpoint.

