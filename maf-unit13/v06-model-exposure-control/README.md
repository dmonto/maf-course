# Model exposure control

Validate an exact approved Azure OpenAI host. Reject HTTP, deceptive hosts, user info, non-root paths, queries, fragments and an alternate port. No network configuration is changed.

This is an independent entry project with stable implementation in `Shared/Unit13.Common`. Run it with the entire unit directory present. Configuration comes from the single root `.env`, located from the executable path.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v06 -Mode fixture
```

Expected process exit: `0` when all assertions in this demonstration hold. Configuration/setup errors return `2`; an actual failed suite or gate returns `1`. v09 deliberately invokes a negative gate and expects `1` before returning successful demonstration status `0`.

Artifacts are written under `artifacts/v06/run-<timestamp>-<id>/`. The final `RESULT` line identifies the exact directory. A run never silently reuses earlier evidence.
