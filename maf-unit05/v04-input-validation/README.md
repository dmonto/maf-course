# v04: Input validation

Executes the tool adapter directly against normalized input, invalid syntax, inaccessible CRM, absent billing, and the shared attempt budget.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v04 -Demo
```

For the assigned Azure deployment, fill in the root `.env`, run `Scripts/Login-Azure.ps1` once, then replace `-Demo` with `-Live`. v04 performs no inference in either mode.

## Expected result

No model calls occur in either mode. Expect ok, invalid-input, not-available and not-available, then tool-budget-exceeded on the fourth recorded attempt shared across both tools.

The demo client is explicitly labelled deterministic synthetic software. It exercises the real MAF tool and typed-output pipeline but does not measure Azure connectivity, model behavior or prompt quality. Live failures do not fall back to demo output.

## Shared source

`Program.cs` is this section's executable entry point and calls the relevant branch of `Shared/Unit05.Common/SectionExamples.cs`. The project references `Unit05.Common`. Every project loads the same root `.env`, prompt files and pinned dependencies. Keep the complete extracted package together.
