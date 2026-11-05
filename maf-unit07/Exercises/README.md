# Possible instructor solutions

These projects implement the two exercise statements in Unit 7. Keep the example projects as the student baseline. The solutions reference the shared baseline library, and only the additions live here.

Run all commands from `maf-unit07`. Build `Maf.Unit07.slnx` first. The root `.env` is shared with every example.

## Exercise 1: evidence-quality gate

`ex01-evidence-quality-gate` introduces a timestamped message, an observation-timestamp executor and a separate validation executor between evidence retrieval and drafting. `--stale` makes the synthetic observation 30 minutes old. The gate rejects observations older than five minutes or in the future.

```powershell
& ./Scripts/Run-Example.ps1 -Example ex01 -Service vpn -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example ex01 -Service payroll -Decision yes -Demo -NoBuild

dotnet run --no-build --project ./Exercises/ex01-evidence-quality-gate/Unit07.EX01.csproj -- run payroll --stale --demo
if ($LASTEXITCODE -eq 0) { throw 'Stale evidence was accepted.' }
```

Fresh evidence preserves both original routes. Stale evidence fails in `evidence-quality-gate`, before `draft` and before any review. Expect no `RESULT`. A production observation timestamp must come from the authoritative source; this solution uses a synthetic timestamp to exercise the boundary.

## Exercise 2: idempotent escalation and recovery

`ex02-idempotent-escalation` separates the review decision from the local write. Rejection yields an outcome without sending a write message. Approval sends a typed `ApprovedEscalation` to `write-escalation`.

The SQLite repository uses the key `INC-DEMO-001:escalate:v1` as its primary key. In a transaction it inserts once or reads the existing receipt. Different state directories contain independent repositories, so use the same `-State` when testing deduplication or recovery.

```powershell
$state = 'operation-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
& ./Scripts/Run-Example.ps1 -Example ex02 -Decision no -State $state -Demo -NoBuild
# Rejection creates zero records. Approval below creates the first record.
& ./Scripts/Run-Example.ps1 -Example ex02 -Decision yes -State $state -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example ex02 -Decision yes -State $state -Demo -NoBuild
```

The approved operations return the same escalation ID. The second reports `reused=true`. The operation-receipt event reports a row count of one.

### Fail after the database commit and recover

```powershell
$state = 'recover-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
dotnet run --no-build --project ./Exercises/ex02-idempotent-escalation/Unit07.EX02.csproj -- run payroll --decision yes --fail-after-save --state $state --demo
if ($LASTEXITCODE -eq 0) { throw 'Failure injection unexpectedly succeeded.' }

& ./Scripts/Run-Example.ps1 -Example ex02 -Command resume -State $state -Demo -NoBuild
```

The first invocation commits one record, emits its operation receipt, then deliberately fails before yielding `escalation-created`. The host retains a recovery bookmark. The previous successful framework checkpoint contains the approved write message. Resume without the failure flag replays the write, reuses the existing receipt and returns `escalation-created` without another review.

The approved proposal and pending write are restored from the framework checkpoint; the external-effect receipt is read from SQLite. A checkpoint does not roll back the committed database write. `Verification/verify.py` checks zero rows on rejection, one row after recovery and an unchanged escalation ID on replay.

This is a synthetic local escalation repository. It does not call a ticketing service or send notifications. The fixed case and operation key intentionally model one escalation per case/operation; a real system needs an operation identity policy covering distinct legitimate requests.
