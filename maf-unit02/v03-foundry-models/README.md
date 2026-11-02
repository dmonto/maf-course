# Compare ticket classifiers in Foundry

GitBook section: **Use of models hosted in Microsoft Foundry**.

Classifies the same three synthetic tickets with two assigned Foundry model deployments. Uses incident, service request and question labels.

## Configuration

Set FOUNDRY_MODELS_BASE_URL to a supported HTTPS /openai/v1/ endpoint, FOUNDRY_MODEL_A and FOUNDRY_MODEL_B. FOUNDRY_AUTH_MODE=entra uses Azure CLI with the ai.azure.com scope; key mode reads FOUNDRY_MODELS_API_KEY. Select deployments supporting this OpenAI-compatible Chat Completions API; catalog availability alone is insufficient.

All settings come from the single root `.env`; existing process variables override that file. Keep this folder together with `Shared` and the root build files.

## Run from the ZIP root

```powershell
dotnet run --project ./v03-foundry-models/FoundryClassifiers.csproj -- --demo
dotnet run --project ./v03-foundry-models/FoundryClassifiers.csproj
```

`--demo` uses explicitly synthetic responses and usage and does not call any model. The second command uses the configured services except for the pure cost calculator.


## Expected result

Six labeled responses, exact-label checks and correct/total counts per deployment. Incorrect labels are recorded as quality failures, separate from connection failures.

The program saves its report under the root `output` directory. An API or configuration failure exits nonzero; quality checks are reported separately. See the root README for restore, Azure sign-in and Foundry Local preparation.
