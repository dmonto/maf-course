# v08: Temperature and parameter control

Repeats the exact controlled comparison from Temperature and parameter control: one request, v1 then v2, fixed deployment and generation settings.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v08 -Demo
```

For the assigned Azure deployment, fill in the root `.env`, run `Scripts/Login-Azure.ps1` once, then replace `-Demo` with `-Live`. v04 performs no inference in either mode.

## Expected result

Temperature and TopP remain unset. Reports retain MaxOutputTokens, MaxIterations and TimeoutSeconds. Differences observed with the scripted demo are not a model-quality estimate.

The demo client is explicitly labelled deterministic synthetic software. It exercises the real MAF tool and typed-output pipeline but does not measure Azure connectivity, model behavior or prompt quality. Live failures do not fall back to demo output.

## Shared source

`Program.cs` is this section's executable entry point and calls the relevant branch of `Shared/Unit05.Common/SectionExamples.cs`. The project references `Unit05.Common`. Every project loads the same root `.env`, prompt files and pinned dependencies. Keep the complete extracted package together.

This example deliberately compares both versions regardless of the default PROMPT_VERSION. Use v06 or v07 for a single selected version.
