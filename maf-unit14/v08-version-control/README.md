# v08: Version control

Actual assembly SHA-256 and release metadata are recorded without inventing an image digest.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v08 -Mode fixture -NoBuild
```

Expected: `RESULT` has `passed=true` and the exact new artifact directory. Stable implementation is in `Shared/Unit14.Common`. This project uses the single unit-root `.env`.

`release-evidence.json` includes the SHA-256 of the actual loaded `Unit14.Common.dll`, release, prompt version and any assigned model/configuration metadata. Its artifact kind is `dotnet-assembly`; the record explicitly does not assert that this is an OCI image digest.

Publish a container using `Scripts/Build-Container.ps1 -PushImage` to obtain a real ACR digest. Use `Capture-Release.ps1` after successful hosted verification to retain a recovery snapshot.
