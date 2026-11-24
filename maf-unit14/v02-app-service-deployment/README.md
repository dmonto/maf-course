# v02: Deployment to App Service

An offline sitecontainers plan records port, identity and digest requirements.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v02 -Mode fixture -NoBuild
```

Expected: `RESULT` has `passed=true` and the exact new artifact directory. Stable implementation is in `Shared/Unit14.Common`. This project uses the single unit-root `.env`.

`appservice-plan.json` specifies a main container on port 8080, system identity for image pulls, managed identity for model calls and the readiness path. Missing image assignment remains null; it is not replaced by a fabricated registry digest.

```powershell
.\Scripts\Deploy-AppService.ps1 -Mode fixture
.\Scripts\Deploy-AppService.ps1 -Mode fixture -Apply
```

The actual apply path requires the assigned existing infrastructure and `APPROVED_IMAGE`. See the root README for hosted probing and Azure mode.
