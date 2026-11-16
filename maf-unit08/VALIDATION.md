# Validation performed

Prepared against the current Unit 8 GitBook content on 2026-11-06.

* Built the complete 14-project solution with .NET SDK 10.0.100: zero warnings and zero errors. This includes the shared library, ten examples, two exercise solutions and the verification executable.
* Ran 24 process cases through `Verification/verify.py`. The twelve default entry points passed, including the expected review exit from v03. Additional cases covered each baseline route, serial scheduling, required-work failure, unsupported observations, invalid arguments, three concurrent cases and denied identity evidence.
* Launched process cases from unrelated temporary working directories. Each executable found the single root `.env`; no example folder contained a settings copy.
* Ran 33 C# contract and concurrency checks. These covered source/status/action validation, report binding, source-list checks, cancellation, an actual specialist deadline, partial-failure preservation, concurrent case isolation, authorization before retrieval, role-scoped envelopes, concurrent repeated deliveries, one synthesis reservation and a conflict arriving during synthesis.
* Executed `Scripts/Test-Examples.ps1 -NoBuild` using PowerShell 7.5.2 on Linux. It ran all ten example entry points, both exercise solutions and the C# checks successfully. The scripts use Windows PowerShell-compatible constructs; Windows PowerShell 5.1 was not executed in this environment.
* Verified a deliberately unconfigured live invocation fails before any specialist starts and does not fall back to demo advice.

The full process results are in [Verification/last-run.json](Verification/last-run.json). Live Azure authentication, deployment capabilities, generated language and Windows-specific behavior require validation in the assigned environment. No live model request or external business action was performed during this verification.

The deterministic paths verify application behavior. They do not establish that an LLM will follow the instructions or that every generated sentence will be grounded. Review live facts and recommendations against the supplied observations.

The exercise reducer is in memory. Its synchronization checks establish behavior within one process, including concurrent callers; they do not establish durable exactly-once delivery across replicas or restarts.
