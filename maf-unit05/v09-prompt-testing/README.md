# v09: Prompt testing

Runs the nine-case acceptance matrix for both instruction versions, using fresh tools and evidence for every case.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v09 -Demo
```

For the assigned Azure deployment, fill in the root `.env`, run `Scripts/Login-Azure.ps1` once, then replace `-Demo` with `-Live`. v04 performs no inference in either mode.

## Expected result

Eighteen run reports per pass. Candidate v2 failures determine the process exit code; baseline v1 failures remain recorded. Case results are saved under output/v09-results.json; semantic review remains required.

The demo client is explicitly labelled deterministic synthetic software. It exercises the real MAF tool and typed-output pipeline but does not measure Azure connectivity, model behavior or prompt quality. Live failures do not fall back to demo output.

## Shared source

`Program.cs` is this section's executable entry point and calls the relevant branch of `Shared/Unit05.Common/SectionExamples.cs`. The project references `Unit05.Common`. Every project loads the same root `.env`, prompt files and pinned dependencies. Keep the complete extracted package together.

## Repeat the comparison

```powershell
& ./Scripts/Run-Example.ps1 -Example v09 -Live -Repeat 2
```

Two passes make 36 inference requests, each potentially containing multiple model service calls. Keep the deployment, data and settings fixed. For the topic's PowerShell implementation of the same nine cases, run Scripts/Test-Prompts.ps1 after building v06.
