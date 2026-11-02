# Prepare and call an assigned Foundry Local model

GitBook section: **Connection to local models**.

Checks the model loaded in the Foundry Local server, then runs the same incident summary locally.

## Configuration

Set FOUNDRY_LOCAL_MODEL to its full device-specific model ID and FOUNDRY_LOCAL_BASE_URL to the loopback /v1/ URL. Run Scripts/Prepare-FoundryLocal.ps1 before the lesson. Install Foundry Local with winget install --id Microsoft.FoundryLocal --exact; reopen PowerShell after installation.

All settings come from the single root `.env`; existing process variables override that file. Keep this folder together with `Shared` and the root build files.

## Run from the root

```powershell
dotnet run --project ./v04-foundry-local/LocalModel.csproj -- --demo
dotnet run --project ./v04-foundry-local/LocalModel.csproj
```

`--demo` uses explicitly synthetic responses and usage and does not call any model. The second command uses the configured services except for the pure cost calculator.


## Expected result

The configured full model ID appears in /openai/loadedmodels, followed by a summary and metadata. Download dependencies, execution providers and weights before disconnecting the network for a real offline test.

The program saves its report under the root `output` directory. An API or configuration failure exits nonzero; quality checks are reported separately. See the root README for restore, Azure sign-in and Foundry Local preparation.
