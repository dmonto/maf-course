# Formal definition of an agent

GitBook example: **Define a bounded capability**.

Calls the assigned Azure deployment with `-Live`; uses a labelled synthetic client with `-Demo`.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v01 -Demo -NoBuild
```

After the root restore/build, this direct command also works:

```powershell
dotnet run --project ./v01-bounded-capability/BoundedCapability.csproj --no-build -- --demo
```

## Expected observations

Payroll returns catalog evidence. A restart request cannot execute a write operation.

To supply one question, use `-InputText 'What is the current status of VPN?'`.

Settings and unchanged logic come from the root `.env` and `Shared/Unit03.Common`. No configuration file is copied into this project. Live wording can vary; inspect structured evidence and boundaries. Default noninteractive runs save JSON reports to the package's `output/` directory; interactive runs print audit events to the console.
