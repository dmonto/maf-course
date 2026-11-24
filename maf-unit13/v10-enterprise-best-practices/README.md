# Enterprise best practices

Combine the case suite, protected evidence projection and Python gate in one release-evidence flow. Supports fixture and Azure. A successful local run does not certify a hosted identity boundary or deployed governance policies.

This is an independent entry project with stable implementation in `Shared/Unit13.Common`. Run it with the entire unit directory present. Configuration comes from the single root `.env`, located from the executable path.

From the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v10 -Mode fixture
```

Expected process exit: `0` when all assertions in this demonstration hold. Configuration/setup errors return `2`; an actual failed suite or gate returns `1`. v09 deliberately invokes a negative gate and expects `1` before returning successful demonstration status `0`.

Artifacts are written under `artifacts/v10/run-<timestamp>-<id>/`. The final `RESULT` line identifies the exact directory. A run never silently reuses earlier evidence.

For a real MAF call, complete the shared Azure settings, sign in, then:

```powershell
.\Scripts\Login-Azure.ps1
.\Scripts\Run-Example.ps1 -Example v10 -Mode azure
```

This uses the configured deployment and makes Azure model calls. Fixture mode does not test inference. Caller identities remain synthetic; the Azure CLI identity authenticates the workload to Azure OpenAI.
