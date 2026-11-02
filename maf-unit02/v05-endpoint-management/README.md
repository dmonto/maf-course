# Run the complete provider connection probe

GitBook section: **Endpoint management**.

Uses a configurable Azure, Foundry Models or Foundry Local connection with the same summary instructions and operational metadata. Includes the optional Python MAF reference.

## Configuration

Use --provider azure, foundry or foundry-local to override MAF_MODEL_PROVIDER. Azure expects a resource root; Foundry Models expects /openai/v1/; Foundry Local expects loopback /v1/. Local model IDs and Azure deployment names are separate identifiers.

All settings come from the single root `.env`; existing process variables override that file. Keep this folder together with `Shared` and the root build files.

## Run from the ZIP root

```powershell
dotnet run --project ./v05-endpoint-management/ProviderProbe.csproj -- --demo
dotnet run --project ./v05-endpoint-management/ProviderProbe.csproj
```

`--demo` uses explicitly synthetic responses and usage and does not call any model. The second command uses the configured services except for the pure cost calculator.


```powershell
dotnet run --project ./v05-endpoint-management/ProviderProbe.csproj -- --provider azure
dotnet run --project ./v05-endpoint-management/ProviderProbe.csproj -- --provider foundry-local
dotnet run --project ./v05-endpoint-management/ProviderProbe.csproj -- --provider foundry-local --prompt 'Synthetic incident: payroll batch processing is delayed. No recovery time is available. Summarize it for support.'
```

The root default is Foundry Local. The fact checker is specific to the payroll incident; a custom unrelated prompt requires corresponding quality criteria.

See [Python/README.md](Python/README.md) for the optional Python reference.

## Expected result

A nonempty answer and status=ok indicate connection success. qualityPassed checks the payroll facts independently. Missing token values remain null. Timing covers the SDK request, not all process startup.

The program saves its report under the root `output` directory. An API or configuration failure exits nonzero; quality checks are reported separately. See the root README for restore, Azure sign-in and Foundry Local preparation.
