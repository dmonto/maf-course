# Change the deployment while preserving the task

GitBook section: **Integration with Azure OpenAI**.

Runs the same payroll incident and instructions against CHAT_FAST_DEPLOYMENT and CHAT_QUALITY_DEPLOYMENT.

## Configuration

Set AZURE_OPENAI_ENDPOINT, CHAT_FAST_DEPLOYMENT and CHAT_QUALITY_DEPLOYMENT, then sign in with Scripts/Login-Azure.ps1. The deployments must be distinct and support Chat Completions.

All settings come from the single root `.env`; existing process variables override that file. Keep this folder together with `Shared` and the root build files.

## Run from the ZIP root

```powershell
dotnet run --project ./v02-azure-openai/AzureDeployments.csproj -- --demo
dotnet run --project ./v02-azure-openai/AzureDeployments.csproj
```

`--demo` uses explicitly synthetic responses and usage and does not call any model. The second command uses the configured services except for the pure cost calculator.


## Expected result

Two responses and two metadata records. Check payroll, delayed batch processing and unknown recovery time. Fluent output alone does not pass the checks.

The program saves its report under the root `output` directory. An API or configuration failure exits nonzero; quality checks are reported separately. See the root README for restore, Azure sign-in and Foundry Local preparation.
