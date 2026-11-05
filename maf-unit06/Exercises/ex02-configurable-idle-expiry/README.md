# Exercise 2: configurable idle expiry

`IdlePolicy.Read` validates `MAF_MEMORY_IDLE_SECONDS` before an agent is created. Missing settings default to 86,400 seconds. Valid values are 60 through 86,400; zero, negative numbers, nonnumeric text and 86,401 are rejected. The baseline examples continue using their fixed 24-hour policy.

## Reproducible expiry check

From the package root:

```powershell
& ./Scripts/Run-Example.ps1 -Example ex02 -Demo -NoBuild -VerifyExpiry
```

This explicit demo check uses a controlled clock, commits one turn, advances beyond the configured deadline and attempts a second turn. Expect `expiredOutcome=memory-expired`, `noModelCallAfterExpiry=true`, `noAuditAdded=true` and unchanged revision 1. No real-time waiting is needed. The controlled clock is used only in this selected verification path.

## Real elapsed-time check

```powershell
$env:MAF_MEMORY_IDLE_SECONDS = '60'
$session = 'ttl-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
& ./Scripts/Run-Example.ps1 -Example ex02 -Demo -NoBuild -Session $session
Start-Sleep -Seconds 65
# This attempt is expected to fail with memory-expired.
& ./Scripts/Run-Example.ps1 -Example ex02 -Demo -NoBuild -Session $session `
    -InputText 'Which error did I report?'
```

The runner throws for the expected nonzero exit. After inspecting the error, run this as a separate command:

```powershell
& ./Scripts/Run-Example.ps1 -Example ex02 -Demo -NoBuild -Session $session -Audit
Remove-Item Env:MAF_MEMORY_IDLE_SECONDS
```

Expect one successful-turn audit row. Use -Live for the initial real Azure turn; expiry is still checked before a second model invocation. Clear the process variable afterwards so other runs can use the root file's value.

For an invalid-setting probe, set `$env:MAF_MEMORY_IDLE_SECONDS='0'`, run ex02 and inspect `configuration-error`; clear the process variable afterwards. `Verification` covers all four invalid categories.

## Separate cleanup policy

Expiry blocks restoration; it does not delete the SQLite row or its metadata. A production cleanup policy should remove expired conversation content on a defined schedule, retain only justified audit metadata, expire backups under the same approved lifecycle and honor any required holds. It must also prevent a stale worker from resurrecting a deleted session, for example through a tombstone or generation-based fencing. Cleanup is not implemented by this exercise solution.
