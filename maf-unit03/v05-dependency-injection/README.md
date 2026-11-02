# Dependency injection

GitBook example: **Complete conversational service-status agent**.

Calls the assigned Azure deployment with `-Live`; uses a labelled synthetic client with `-Demo`.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v05 -Demo -NoBuild
```

After the root restore/build, this direct command also works:

```powershell
dotnet run --project ./v05-dependency-injection/DependencyInjection.csproj --no-build -- --demo
```

## Expected observations

The five prompts run in order. CRM is not exposed; billing is missing; restarting is unavailable. The alternate catalog changes the returned evidence without changing agent wiring.

Add `-Interactive` to use `/new`, `/exit` and your own synthetic questions.

Use `-Case alternate` to inject the alternative catalog; the default is `-Case catalog`.

Settings and unchanged logic come from the root `.env` and `Shared/Unit03.Common`. No configuration file is copied into this project. Live wording can vary; inspect structured evidence and boundaries. Default noninteractive runs save JSON reports to the package's `output/` directory; interactive runs print audit events to the console.
