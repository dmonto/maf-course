# Tool registration

GitBook example: **Separate schema from enforcement**.

Always local-only: registers and invokes an AIFunction directly. No Azure connection is required.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v02 -Demo -NoBuild
```

After the root restore/build, this direct command also works:

```powershell
dotnet run --project ./v02-tool-registration/ToolRegistration.csproj --no-build -- --demo
```

## Expected observations

The schema contains only serviceName. Payroll succeeds; CRM and billing return not-available; a malformed name returns invalid-input. No model requests are made.

Settings and unchanged logic come from the root `.env` and `Shared/Unit03.Common`. No configuration file is copied into this project. Live wording can vary; inspect structured evidence and boundaries. Default noninteractive runs save JSON reports to the package's `output/` directory; interactive runs print audit events to the console.
