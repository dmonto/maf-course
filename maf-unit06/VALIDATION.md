# Validation record

Date: 2026-11-05. Runtime: .NET SDK 10.0.100 on Linux x64. Source: current GitBook Unit 6 read on the same date.

## Results

- All eleven projects in Maf.Unit06.slnx built successfully: seven examples, two exercise solutions, the shared library and the boundary-check executable. Zero warnings and zero errors.
- Seventeen cross-process checks passed: same/fresh session recall, SQLite commit and second-process restoration, function-call/result preservation, real MAF tool invocation, fresh synthetic status lookup, independent replica processes, deterministic stale-write rejection, committed-turn auditing, confirmed-fact provenance, typed-fact survival after pruning, expiry before inference, four invalid lifetime settings and the 60-second valid setting.
- Twenty-one additional repository checks passed against real SQLite and the real MAF session/serialization pipeline. They cover typed state, tenant/owner and audit separation, parameterized lookup, duplicate creation, rollback of both revision and payload on audit failure, current tool ownership/case checks, lifetime boundaries, expired restoration and incompatible schema refusal.
- Two additional process checks passed: a stored demo session rejected a live-provider compatibility mismatch before constructing a live client; the package's shared .env and database resolved when its executable was launched from outside the package directory.
- All five PowerShell scripts parsed successfully using the PowerShell parser. Their full execution under Windows PowerShell was not performed in this environment.

Total: forty behavioral/storage/process checks, plus compilation and PowerShell syntax validation.

## Reproduce the available checks

From the package root:

```powershell
dotnet restore ./Maf.Unit06.slnx --locked-mode
dotnet build ./Maf.Unit06.slnx --no-restore -m:1
& ./Scripts/Test-Examples.ps1 -NoBuild
& ./Scripts/Test-Memory.ps1
dotnet run --project ./Verification/Unit06.Checks.csproj --no-build -- --demo
```

Verification prints `checks=21` on success and creates a uniquely named disposable database under ignored `output/`. The PowerShell examples create synthetic conversation data under ignored `data/`.

## Scope

The deterministic client is explicitly selected and labelled. MAF manages actual sessions, serialized state, history and registered function invocation; SQLite performs actual transactions and conditional writes. This validates those application mechanics without claiming model reasoning quality.

No authenticated request to the user's Azure deployment was performed. Live endpoint access, Azure CLI identity, deployment capabilities and actual model behavior require the assigned environment. Run Test-Memory.ps1 -Live after configuring the common .env and signing in.

v05 is a same-machine simulation of independent replica processes using one local SQLite database. No multi-host backend or deployment was created. Expiry tests do not implement physical deletion, backup retention or distributed fencing.
