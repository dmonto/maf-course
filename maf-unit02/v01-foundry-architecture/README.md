# Identify the execution boundary

GitBook section: **Azure AI Foundry architecture**.

Calls an in-process synthetic incident function, describes the application/model/project boundaries, then summarizes the incident.

## Configuration

Use MAF_MODEL_PROVIDER and its model/endpoint settings. No App Service or server-managed agent is created.

All settings come from the single root `.env`; existing process variables override that file. Keep this folder together with `Shared` and the root build files.

## Run from the root

```powershell
dotnet run --project ./v01-foundry-architecture/BoundaryDemo.csproj -- --demo
dotnet run --project ./v01-foundry-architecture/BoundaryDemo.csproj
```

`--demo` uses explicitly synthetic responses and usage and does not call any model. The second command uses the configured services except for the pure cost calculator.


## Expected result

A boundary description, three-line incident summary and metadata record; the synthetic incident function runs in the current process.

The program saves its report under the root `output` directory. An API or configuration failure exits nonzero; quality checks are reported separately. See the root README for restore, Azure sign-in and Foundry Local preparation.
