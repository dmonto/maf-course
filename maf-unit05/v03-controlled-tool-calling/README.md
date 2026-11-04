# v03: Controlled tool calling

Runs the four narrative tool-selection examples: status, guidance, both, and missing subject.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v03 -Demo
```

For the assigned Azure deployment, fill in the root `.env`, run `Scripts/Login-Azure.ps1` once, then replace `-Demo` with `-Live`. v04 performs no inference in either mode.

## Expected result

Status uses GetServiceStatus; guidance uses GetSupportGuidance; the combined case uses both; the missing-subject case requests clarification with no tool call. Every case has fresh tools and evidence.

The demo client is explicitly labelled deterministic synthetic software. It exercises the real MAF tool and typed-output pipeline but does not measure Azure connectivity, model behavior or prompt quality. Live failures do not fall back to demo output.

## Shared source

`Program.cs` is this section's executable entry point and calls the relevant branch of `Shared/Unit05.Common/SectionExamples.cs`. The project references `Unit05.Common`. Every project loads the same root `.env`, prompt files and pinned dependencies. Keep the complete extracted package together.
