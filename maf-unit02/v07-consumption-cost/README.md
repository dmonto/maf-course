# Distinguish call cost from case cost

GitBook section: **Consumption cost control**.

Reproduces the GitBook calculation with 1,500 input tokens, 250 output tokens and hypothetical $0.40/$1.60 rates per million tokens.

## Configuration

Runs without inference or credentials. The rates are teaching assumptions, not current prices. Adapt the explicit constants for another calculation.

All settings come from the single root `.env`; existing process variables override that file. Keep this folder together with `Shared` and the root build files.

## Run from the ZIP root

```powershell
dotnet run --project ./v07-consumption-cost/CostCalculator.csproj -- --demo
dotnet run --project ./v07-consumption-cost/CostCalculator.csproj
```

`--demo` uses explicitly synthetic responses and usage and does not call any model. The second command uses the configured services except for the pure cost calculator.


## Expected result

USD 0.001 per call, USD 10 for 10,000 calls and USD 30 for 10,000 three-call cases. An estimate with unavailable input usage remains null.

The program saves its report under the root `output` directory. An API or configuration failure exits nonzero; quality checks are reported separately. See the root README for restore, Azure sign-in and Foundry Local preparation.
