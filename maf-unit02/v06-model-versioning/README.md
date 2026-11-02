# Validate a candidate before switching

GitBook section: **Model versioning**.

Runs three fixed incidents against baseline and candidate deployments, records metadata and recommends retaining the baseline or reviewing the candidate.

## Configuration

Set CHAT_FAST_DEPLOYMENT and CHAT_QUALITY_DEPLOYMENT plus BASELINE_* and CANDIDATE_* model metadata from the actual deployments. Also record APPLICATION_COMMIT and INSTRUCTIONS_VERSION. Run Azure login first.

All settings come from the single root `.env`; existing process variables override that file. Keep this folder together with `Shared` and the root build files.

## Run from the root

```powershell
dotnet run --project ./v06-model-versioning/ModelRegression.csproj -- --demo
dotnet run --project ./v06-model-versioning/ModelRegression.csproj
```

`--demo` uses explicitly synthetic responses and usage and does not call any model. The second command uses the configured services except for the pure cost calculator.


## Expected result

Demo mode deliberately injects an invented recovery time into one candidate response and recommends keep-baseline. Live mode requires passing fact checks and supplied model metadata for candidate eligibility; the program does not change deployments.

The program saves its report under the root `output` directory. An API or configuration failure exits nonzero; quality checks are reported separately. See the root README for restore, Azure sign-in and Foundry Local preparation.
