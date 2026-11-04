# v07: Prompt management

The full host with the shared v1 and v2 instruction files. Start with -PromptVersion v2 to load the candidate. /health and scopes report v2. Restart to load changes. The candidate adds a Catalog status: prefix after successful lookups; demo mode does not evaluate prompt quality.

## Run from the package root

Terminal 1:

```powershell
& ./Scripts/Run-Example.ps1 -Example v07 -Demo
```

Terminal 2:

```powershell
& ./Scripts/Test-Example.ps1 -Example v07
```

For live Azure inference, set the assigned endpoint and deployment in the root `.env`, run `Scripts/Login-Azure.ps1`, and replace `-Demo` with `-Live`. v01 performs no inference in either mode.

Stop the previous host with Ctrl+C before starting another example on port 5050. For another port, pass `-Port 5051` to the launcher and `-BaseUrl http://127.0.0.1:5051` to the test script.

## Files and expected evidence

`Program.cs` is this section's executable entry point. Its project references `Shared/Unit04.Common`; all projects use the root `.env`, `appsettings.json` and `Prompts`. The full extracted directory is required.

The full host with the shared v1 and v2 instruction files. Start with -PromptVersion v2 to load the candidate. /health and scopes report v2. Restart to load changes. The candidate adds a Catalog status: prefix after successful lookups; demo mode does not evaluate prompt quality.

The demo client is deterministic synthetic software inside the real MAF function/session pipeline. It does not establish Azure connectivity or model quality. Live inference failures are reported; there is no automatic switch to demo mode.

## Select the candidate prompt

```powershell
& ./Scripts/Run-Example.ps1 -Example v07 -Live -PromptVersion v2
```

Repeat the same test inputs used with v1. File edits are loaded only when the application starts.
