# Conversational context management

GitBook example: **Follow-up context and fresh evidence**.

Calls the assigned Azure deployment with `-Live`; uses a labelled synthetic client with `-Demo`.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v03 -Demo -NoBuild
```

After the root restore/build, this direct command also works:

```powershell
dotnet run --project ./v03-conversational-context/ConversationalContext.csproj --no-build -- --demo
```

## Expected observations

The first two turns share a conversation and payroll subject. After /new, the old subject is absent and clarification is required.

Settings and unchanged logic come from the root `.env` and `Shared/Unit03.Common`. No configuration file is copied into this project. Live wording can vary; inspect structured evidence and boundaries. Default noninteractive runs save JSON reports to the package's `output/` directory; interactive runs print audit events to the console.
