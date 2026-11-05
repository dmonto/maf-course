# Validation

Validated on 2026-11-05 with .NET SDK 10.0.403 on Linux. The package targets .NET 10 and includes `global.json` plus per-project package lock files.

- All twelve projects (nine examples, two exercise solutions and one shared library) built with zero warnings and zero errors.
- Locked dependency restoration was checked against the included lock files.
- `Verification/verify.py` passed 21 process-level acceptance checks. Its captured result is `Verification/last-run.json`.
- `Scripts/Test-Examples.ps1 -NoBuild` passed its deterministic smoke checks in PowerShell 7.6.9.
- Every PowerShell script passed PowerShell's syntax parser. The example wrapper was executed in demo mode.

The checks cover healthy and degraded routes, approval and rejection, three exhausted timeout attempts, invalid input rejected before evidence reads, executor order, parallel/serial evidence collection, checkpoint pause and new-process resume, stale-evidence rejection, zero rows on rejection, and one durable escalation with the same ID after post-write recovery and repeated approval.

The measured evidence-read durations in the captured Python run were 507 ms for concurrent reads and 811 ms for sequential reads with configured delays of 300 ms and 500 ms. Timing is observational and is not used as a machine-speed assertion.

Demo mode still uses the real MAF workflow, request-port and checkpoint implementation. Only the language-generation step is deterministic C# text. Live Azure inference, Azure CLI login/RBAC and Windows PowerShell 5.1 execution were not exercised with course credentials. No external ticketing API, DevUI host or OpenTelemetry exporter is configured by these examples.
