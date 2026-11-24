# v09: Environment management

The same release is mapped to local, verification and production identity contracts.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v09 -Mode fixture -NoBuild
```

Expected: `RESULT` has `passed=true` and the exact new artifact directory. Stable implementation is in `Shared/Unit14.Common`. This project uses the single unit-root `.env`.

`environment-matrix.json` makes the narrative environment example executable: the same release/prompt is mapped to local, App Service, AKS and production identity contracts. The file states that no environment was provisioned.

The sample always reads synthetic service evidence. Real corporate adapters, caller authorization, network isolation, data retention and production identity grants remain environment responsibilities. Promotion uses the same approved image digest with environment-specific settings.
