# Result management

GitBook example: **A completed refusal**.

Calls the assigned Azure deployment with `-Live`; uses a labelled synthetic client with `-Demo`.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v06 -Demo -NoBuild
```

After the root restore/build, this direct command also works:

```powershell
dotnet run --project ./v06-result-management/ResultManagement.csproj --no-build -- --demo
```

## Expected observations

The CRM invocation can complete while recordRetrieved is false. Inspect the tool result rather than equating invocation status with business success.

Settings and unchanged logic come from the root `.env` and `Shared/Unit03.Common`. No configuration file is copied into this project. Live wording can vary; inspect structured evidence and boundaries. Default noninteractive runs save JSON reports to the package's `output/` directory; interactive runs print audit events to the console.
