# MAF Unit 8: executable examples

Multi-Agent Orchestration: Coordinator Agents, Specialists and Collaboration Patterns.

Ten independent .NET console projects cover the ten sections of the current GitBook unit. The six sections with blue examples map to their executable behavior; the four narrative sections add small demonstrations of the described contracts, permissions, state boundaries and scheduling patterns. The two possible exercise solutions are separate projects under `Exercises`.

Stable configuration, contracts, agent instructions and orchestration live in `Shared/Unit08.Common`. Each example owns its `Program.cs`, project file and guide. All comments, prompts and documentation are in English.

## Prepare and build

Use the .NET 10 SDK, Git and PowerShell. Copy the `maf-unit08` folder from the ZIP into the root of your existing `maf-course` checkout. For a new checkout:

```powershell
git clone https://github.com/dmonto/maf-course.git
if ($LASTEXITCODE -ne 0) { throw 'Clone failed.' }
Set-Location ./maf-course
# Extract or copy the maf-unit08 folder from the ZIP into this repository root.
Set-Location ./maf-unit08
& ./Scripts/Initialize-Environment.ps1
dotnet restore ./Maf.Unit08.slnx --locked-mode -m:1
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build ./Maf.Unit08.slnx --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& ./Scripts/Run-Example.ps1 -Example v07 -Demo -NoBuild
```

In an existing clone, enter `maf-unit08` after copying the folder and start with `Initialize-Environment.ps1`. This package adds one directory; it does not replace other units.

The default mode uses deterministic C# advice with the same application-owned scheduling as the live team. It requires no Azure resource and makes no model request. It checks orchestration; it does not assess an LLM's reasoning or prompt behavior.

The synthetic facts remain VPN **healthy**, payroll **degraded**, and payroll recovery time **unknown**. A referral is a proposal. No ticket, email, credential reset or production operation is executed.

## One shared .env

The only settings file is `maf-unit08/.env`. The ZIP includes a placeholder-only copy of this file shared by all projects. `Initialize-Environment.ps1` copies the template only when `.env` is absent; applications also initialize it on first use. Keep this file at the unit root and keep the whole package together. Executables locate it from their assembly directory even when launched from a different working directory.

```dotenv
MAF_EXECUTION_MODE=demo
AZURE_OPENAI_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT_NAME=YOUR-ASSIGNED-DEPLOYMENT
TENANT_ID=YOUR-TENANT-ID
SUBSCRIPTION_ID=YOUR-SUBSCRIPTION-ID
SPECIALIST_TIMEOUT_SECONDS=30
COORDINATOR_TIMEOUT_SECONDS=30
RUN_TIMEOUT_SECONDS=90
SPECIALIST_MAX_OUTPUT_TOKENS=300
COORDINATOR_MAX_OUTPUT_TOKENS=400
SPECIALIST_CONCURRENCY=2
NETWORK_DELAY_MS=150
SERVICE_DELAY_MS=250
```

Nonempty process environment values take precedence over the file. `--demo` and `--live`, or the corresponding PowerShell switches, override `MAF_EXECUTION_MODE`. Values are literal strings without shell execution or variable interpolation. Clear stale process variables or open a fresh terminal when changing previously imported settings.

The real `.env`, build output and local data are ignored by Git. The template contains no credentials. The model package versions match the current GitBook listings; transitive dependencies are pinned by `packages.lock.json` files. `global.json` accepts compatible .NET 10 feature bands.

## Projects and expected behavior

Run commands in this table from the `maf-unit08` root. The runner's `-Example` accepts the short ID or full directory name.

| ID and directory | GitBook section | Expected result |
| --- | --- | --- |
| `v01-coordinator-architecture` | Coordinator-specialist architecture | Responsibilities, selected plan and a completed VPN information outcome |
| `v02-task-delegation` | Task delegation | Scoped assignments, accepted reports and zero coordinator invocations |
| `v03-conflict-resolution` | Conflict resolution | Forced unsupported observation; `needs-review`, no synthesis and native exit 2 |
| `v04-inter-agent-communication` | Inter-agent communication | JSON envelope/report round trips and rejection of a forged run binding |
| `v05-role-management` | Role management | Permitted recommendation per role; a reset recommendation is rejected |
| `v06-shared-state` | Shared state control | Two simultaneous cases with separate run/task identities |
| `v07-centralized-supervision` | Centralized supervision | The complete combined case with two reports and one coordinator brief |
| `v08-scalability` | Multi-agent scalability | Concurrent cases, measured host peak and explicit required-work failure |
| `v09-collaborative-patterns` | Collaborative patterns | Sequential and concurrent consultations with timings and the same business stage |
| `v10-coordination-checks` | Coordination best practices | Five-case matrix checks for routing, failures and collection before synthesis |

```powershell
& ./Scripts/Run-Example.ps1 -Example v01 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v02 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v03 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v04 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v05 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v06 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v07 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v08 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v09 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v10 -Demo -NoBuild
```

`v04`, `v05`, `v06` and `v09` make narrative concepts executable. `v04` is an in-process JSON exchange, not an external broker. `v05` validates recommendation policy without granting Entra roles. `v09` uses C# scheduling around MAF agents; it does not claim to implement native handoff, group-chat or Magentic builders.

## Complete application and controlled failures

```powershell
& ./Scripts/Run-Example.ps1 -Example v07 -Scenario vpn -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v07 -Scenario payroll -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v07 -Scenario combined -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v07 -Scenario payroll -Conflict -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v07 -Scenario combined -FailNetwork -Demo -NoBuild
```

VPN returns `information-ready`. Payroll and combined return `escalation-proposal`. An unsupported observation or required specialist failure returns `needs-review` with a null brief. An accepted service report remains visible when the network assignment fails. The coordinator does not receive rejected advice.

`-Conflict` replaces the generated advice status with `recovered`; original source evidence is retained. `v03` enables this injection by default. `-FailNetwork` raises a synthetic timeout before calling the network backend and requires a VPN or combined scenario. Neither switch changes the catalog.

## Concurrency and scheduling

```powershell
& ./Scripts/Run-Example.ps1 -Example v08 -Cases 3 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v08 -Cases 3 -FailNetwork -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v09 -NetworkDelayMs 200 -ServiceDelayMs 350 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v07 -Serial -Demo -NoBuild
```

Each case has its own semaphore, with a maximum of two concurrent specialists. Several cases can therefore produce a host peak above two. This illustrates the need for a separate service-wide admission rule in a hosted application. No distributed limiter or durable state store is installed.

Serial consultations finish one after the other. Concurrent consultations wait for all required reports before one synthesis. The roles are unchanged between these scheduling modes. Compare evidence and stages; exact timings and live phrasing can vary.

Invocation metrics count logical attempts, including an injected timeout before inference. They are not a provider billing counter. Provider HTTP retries and input tokens need separate measurement.

## Live Azure execution

Edit the shared `.env` with the assigned resource-root endpoint, deployment, tenant and subscription. Use an HTTPS Azure OpenAI resource-root URL, not a Foundry project endpoint or an `/openai/v1/` base URL. The deployment must support the structured-output requests used by `RunAsync<Advice>` and `RunAsync<Brief>`.

```powershell
& ./Scripts/Login-Azure.ps1
& ./Scripts/Run-Example.ps1 -Example v07 -Live -NoBuild
```

A successful combined run invokes two specialists and one coordinator. Each call starts a fresh session. The supervisor retains ownership of the report collection and business stage. Live failures never fall back to deterministic advice.

Every live invocation can incur model usage. `v06`, `v08`, `v09` and `v10` perform multiple cases or scheduling comparisons. Start with `v07` and inspect its source IDs, statuses and generated claims before larger runs. Source/action checks cannot mechanically prove every sentence in a generated explanation. Review meaning, especially invented recovery estimates or claims that a referral was executed.

`ex01` is a local policy solution and supports only demo mode. In `ex02`, accepted reports are fixed C# inputs so the exercise isolates delivery semantics; `-Live` invokes only the coordinator once.

## Results, events and exit codes

Every example writes one JSON object to standard output: `Example`, `Mode`, `Stage` and `Data`. Events go to standard error. `v07` places the original case outcome (`RunId`, `Stage`, `Reports`, `Brief`) inside `Data` so all projects share one result wrapper.

| Exit | Meaning |
| --- | --- |
| 0 | The demonstration or verification completed |
| 2 | Required work needs review; inspect the returned business outcome |
| 1 | Invalid input, configuration, cancellation or an invalid result stopped execution |

The PowerShell runner accepts business exit 2 and prints an explanatory line. Direct `dotnet` invocation preserves the native exit code and is suitable for automation:

```powershell
$json = dotnet run --no-build --project ./v07-centralized-supervision/Unit08.V07.csproj -- combined --demo
if ($LASTEXITCODE -ne 0) { throw 'The combined case did not complete.' }
$result = ($json -join "`n") | ConvertFrom-Json
$result.Data.Reports
```

No full exception details, token values or arbitrary user transcripts are emitted in failure events. A service host would add authorized diagnostics and correlation to its own error handling.

## Exercise solutions and validation

See [Exercises/README.md](Exercises/README.md) for the two instructor solutions. Baseline examples do not depend on either solution. The source GitBook remains unchanged.

```powershell
& ./Scripts/Test-Examples.ps1 -NoBuild
```

The script runs all twelve entry points and the C# contract/concurrency checks. Python is optional for additional process-level checks from unrelated working directories:

```powershell
python ./Verification/verify.py
```

See [GITBOOK-MAPPING.md](GITBOOK-MAPPING.md) for source alignment, [VALIDATION.md](VALIDATION.md) for performed validation and [Verification/last-run.json](Verification/last-run.json) for the process results. The read-only source snapshot is under `Reference`.
