# User interaction

GitBook example: **Ambiguity after two lookups**.

Calls the assigned Azure deployment with `-Live`; uses a labelled synthetic client with `-Demo`.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v07 -Demo -NoBuild
```

After the root restore/build, this direct command also works:

```powershell
dotnet run --project ./v07-user-interaction/UserInteraction.csproj --no-build -- --demo
```

## Expected observations

The scripted model asks for clarification when two service references are possible, and after /new. Review live wording instead of expecting exact text.

Add `-Interactive` to use `/new`, `/exit` and your own synthetic questions.

Settings and unchanged logic come from the root `.env` and `Shared/Unit03.Common`. No configuration file is copied into this project. Live wording can vary; inspect structured evidence and boundaries. Default noninteractive runs save JSON reports to the package's `output/` directory; interactive runs print audit events to the console.
