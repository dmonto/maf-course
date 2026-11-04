# v06: Encapsulation of logic in tools

Runs the complete two-tool application through shared configuration, registration, bounded invocation and output validation.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v06 -Demo
```

For the assigned Azure deployment, fill in the root `.env`, run `Scripts/Login-Azure.ps1` once, then replace `-Demo` with `-Live`. v04 performs no inference in either mode.

## Expected result

The default combined payroll request returns degraded status, collect-incident-reference guidance, both synthetic sources and ActionExecuted=false. This is the canonical single-run project used by the PowerShell matrix.

The demo client is explicitly labelled deterministic synthetic software. It exercises the real MAF tool and typed-output pipeline but does not measure Azure connectivity, model behavior or prompt quality. Live failures do not fall back to demo output.

## Shared source

`Program.cs` is this section's executable entry point and calls the relevant branch of `Shared/Unit05.Common/SectionExamples.cs`. The project references `Unit05.Common`. Every project loads the same root `.env`, prompt files and pinned dependencies. Keep the complete extracted package together.

## A custom request

```powershell
& ./Scripts/Run-Example.ps1 -Example v06 -Live -PromptVersion v2 -InputText 'What is the current status of VPN?'
```
