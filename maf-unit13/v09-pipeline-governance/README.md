# Pipeline governance

Run the suite and the original Python gate, then verify that a changed policy version is rejected. The expected negative gate returns 1; the complete successful demonstration returns 0. --report checks an existing artifact directory.

This is an independent entry project with stable implementation in `Shared/Unit13.Common`. Run it with the entire unit directory present. Configuration comes from the single root `.env`, located from the executable path.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v09 -Mode fixture
```

Expected process exit: `0` when all assertions in this demonstration hold. Configuration/setup errors return `2`; an actual failed suite or gate returns `1`. v09 deliberately invokes a negative gate and expects `1` before returning successful demonstration status `0`.

Artifacts are written under `artifacts/v09/run-<timestamp>-<id>/`. The final `RESULT` line identifies the exact directory. A run never silently reuses earlier evidence.

Check a retained report directly:

```powershell
.\Scripts\Run-Example.ps1 -Example v09 -Report 'C:\path with spaces\run-directory'
```

This operation propagates the Python gate exit code. Supply a run with both `audit.jsonl` and `results.json`.
