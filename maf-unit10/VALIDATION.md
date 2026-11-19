# Validation - 7 November 2026

- .NET SDK 10.0.100: all 12 projects built successfully with 0 warnings and 0 errors.
- All 9 demo examples, both instructor solutions and the verification project executed successfully.
- The PowerShell Test-Examples.ps1 suite passed; its deliberate read-only notification denial returned exit code 1 as expected.
- Verification passed 16 checks, including real AIFunction invocation, hidden runtime scope, SQL predicates, captured Slack JSON, concurrent claims, persistence, preview expiry, uncertain replay and legacy validation.
- The concluding acceptance matrix passed 7 checks.
- The local HTTP fixture was launched through Start-Fixture.ps1, exercising Import-Env.ps1, real loopback HTTP, SQLite, explicit review, an acknowledged local receipt, replay suppression and the legacy route.
- Run-Example.ps1 argument forwarding was checked with a locally approved simulated notification.
- Exercise 1 was rerun after its ownership migration; the explicit seed-column list preserves repeatability.
- Azure model inference and real Slack delivery were not invoked. The local demo never contacts those services.

Commands are in the root README and the individual project READMEs. Runtime databases, generated reports, SDKs and build outputs are excluded from the archive.
