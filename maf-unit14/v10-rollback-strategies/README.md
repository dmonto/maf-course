# v10: Rollback strategies

A bad fixture contract is rejected and a restored release is verified.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v10 -Mode fixture -NoBuild
```

Expected: `RESULT` has `passed=true` and the exact new artifact directory. Stable implementation is in `Shared/Unit14.Common`. This project uses the single unit-root `.env`.

The rehearsal starts a real local fixture host labeled `14.2.0` with a deliberately incorrect VPN HTTP status. Its probe fails. It then restores `active-configuration.json` from `approved-configuration.json`, including the previous model-mapping value, starts the previous `14.1.0` contract and verifies that it passes.

The artifact records `cloudRollbackExecuted=false`. For an actual target, capture an approved image/configuration record with `Scripts/Capture-Release.ps1`, inspect `Restore-Release.ps1` without `-Apply`, then apply and repeat the functional probe. External state and delivered business actions require their own recovery process.
