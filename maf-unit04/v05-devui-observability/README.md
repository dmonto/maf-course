# v05: Observability with DevUI

The full host, with a protocol request script and a manual DevUI inspection. Observe ToolCompleted, RequestCompleted and SpanCompleted. Protocol conversations and /api/chat conversations remain separate.

## Run from the package root

Terminal 1:

```powershell
& ./Scripts/Run-Example.ps1 -Example v05 -Demo
```

Terminal 2:

```powershell
& ./Scripts/Test-Example.ps1 -Example v05
```

For live Azure inference, set the assigned endpoint and deployment in the root `.env`, run `Scripts/Login-Azure.ps1`, and replace `-Demo` with `-Live`. v01 performs no inference in either mode.

Stop the previous host with Ctrl+C before starting another example on port 5050. For another port, pass `-Port 5051` to the launcher and `-BaseUrl http://127.0.0.1:5051` to the test script.

## Files and expected evidence

`Program.cs` is this section's executable entry point. Its project references `Shared/Unit04.Common`; all projects use the root `.env`, `appsettings.json` and `Prompts`. The full extracted directory is required.

The full host, with a protocol request script and a manual DevUI inspection. Observe ToolCompleted, RequestCompleted and SpanCompleted. Protocol conversations and /api/chat conversations remain separate.

The demo client is deterministic synthetic software inside the real MAF function/session pipeline. It does not establish Azure connectivity or model quality. Live inference failures are reported; there is no automatic switch to demo mode.
