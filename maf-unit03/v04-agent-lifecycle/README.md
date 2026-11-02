# Agent lifecycle

GitBook example: **Conversation reset**.

Calls the assigned Azure deployment with `-Live`; uses a labelled synthetic client with `-Demo`.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v04 -Demo -NoBuild
```

After the root restore/build, this direct command also works:

```powershell
dotnet run --project ./v04-agent-lifecycle/AgentLifecycle.csproj --no-build -- --demo
```

## Expected observations

The configured agent stays available; /new creates a fresh session and conversation identifier. Interactive mode handles /new and /exit.

Add `-Interactive` to use `/new`, `/exit` and your own synthetic questions.

Settings and unchanged logic come from the root `.env` and `Shared/Unit03.Common`. No configuration file is copied into this project. Live wording can vary; inspect structured evidence and boundaries. Default noninteractive runs save JSON reports to the package's `output/` directory; interactive runs print audit events to the console.
