# v01: Prompt design principles

Compares the minimal v1 and explicit v2 instructions using the same unsupported recovery-time claim.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v01 -Demo
```

For the assigned Azure deployment, fill in the root `.env`, run `Scripts/Login-Azure.ps1` once, then replace `-Demo` with `-Live`. v04 performs no inference in either mode.

## Expected result

Two run reports, identical deployment and limits, separately identified prompt versions and hashes. The expected v2 business fields use degraded status and null RecoveryTime. v1 is not required to fail.

The demo client is explicitly labelled deterministic synthetic software. It exercises the real MAF tool and typed-output pipeline but does not measure Azure connectivity, model behavior or prompt quality. Live failures do not fall back to demo output.

## Shared source

`Program.cs` is this section's executable entry point and calls the relevant branch of `Shared/Unit05.Common/SectionExamples.cs`. The project references `Unit05.Common`. Every project loads the same root `.env`, prompt files and pinned dependencies. Keep the complete extracted package together.

This example deliberately compares both versions regardless of the default PROMPT_VERSION. Use v06 or v07 for a single selected version.
