# Investigate response latency

GitBook section: **Model observability**.

Reuses a configured client for repeated requests and saves status, timing, IDs, usage and quality outcomes. Computes descriptive percentiles with linear interpolation.

## Configuration

Configure the selected provider. OBSERVABILITY_RUNS defaults to 3 and may be 1..100; each live run makes a model request. The first request is labeled first-request, not automatically assumed to be a verified cold start.

All settings come from the single root `.env`; existing process variables override that file. Keep this folder together with `Shared` and the root build files.

## Run from the ZIP root

```powershell
dotnet run --project ./v08-model-observability/ObservabilityDemo.csproj -- --demo
dotnet run --project ./v08-model-observability/ObservabilityDemo.csproj
```

`--demo` uses explicitly synthetic responses and usage and does not call any model. The second command uses the configured services except for the pure cost calculator.


## Expected result

A report with connection successes, quality passes, p50/p95 and per-request evidence. Demo timings are synthetic; a small live sample is not a performance benchmark. No raw content is saved and no telemetry exporter is configured.

The program saves its report under the root `output` directory. An API or configuration failure exits nonzero; quality checks are reported separately. See the root README for restore, Azure sign-in and Foundry Local preparation.
