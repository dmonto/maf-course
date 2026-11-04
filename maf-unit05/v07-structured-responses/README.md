# v07: Structured responses

Requests SupportAnswer through RunAsync<SupportAnswer> and validates the generated business fields against the actual tool results.

## Run from the package root

```powershell
& ./Scripts/Run-Example.ps1 -Example v07 -Demo
```

For the assigned Azure deployment, fill in the root `.env`, run `Scripts/Login-Azure.ps1` once, then replace `-Demo` with `-Live`. v04 performs no inference in either mode.

## Expected result

A valid result has contract-accepted. A field mismatch produces contract-rejected, answer=null and exit code 2. Free-text Summary meaning is not completely checked by AnswerValidator.

The demo client is explicitly labelled deterministic synthetic software. It exercises the real MAF tool and typed-output pipeline but does not measure Azure connectivity, model behavior or prompt quality. Live failures do not fall back to demo output.

## Shared source

`Program.cs` is this section's executable entry point and calls the relevant branch of `Shared/Unit05.Common/SectionExamples.cs`. The project references `Unit05.Common`. Every project loads the same root `.env`, prompt files and pinned dependencies. Keep the complete extracted package together.

## A custom request

```powershell
& ./Scripts/Run-Example.ps1 -Example v07 -Live -PromptVersion v2 -InputText 'What is the current status of VPN?'
```
