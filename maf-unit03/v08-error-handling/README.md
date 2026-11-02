# Error handling

GitBook example: **Diagnose a failure before changing the prompt**.

Calls the assigned Azure deployment with `-Live`; uses a labelled synthetic client with `-Demo`.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v08 -Demo -NoBuild
```

After the root restore/build, this direct command also works:

```powershell
dotnet run --project ./v08-error-handling/ErrorHandling.csproj --no-build -- --demo
```

## Expected observations

Business refusal completes normally. Before-tool fails with zero tool attempts; after-tool fails with one. Timeout and empty output reset the session. Loop requests are bounded.

Demo cases: `all` (default), `business`, `before-tool`, `after-tool`, `timeout`, `empty`, `loop`. Live mode supports `business` only.

Settings and unchanged logic come from the root `.env` and `Shared/Unit03.Common`. No configuration file is copied into this project. Live wording can vary; inspect structured evidence and boundaries. Default noninteractive runs save JSON reports to the package's `output/` directory; interactive runs print audit events to the console.
