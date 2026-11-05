# MAF Unit 7: executable examples

Workflows in MAF: Task Graphs, Decisions and Humans in the Loop.

Nine executable .NET projects cover the examples in the current GitBook unit, including its narrative timeout example. Each project has a separate entry point and state directory. Stable contracts, executors, model setup, dotenv loading and hosting live under `Shared/Unit07.Common`. The two possible exercise solutions live under `Exercises`.

## Install and build

Use .NET 10 SDK, Git and PowerShell. Run these commands from the repository root if it is already cloned; otherwise clone it first.

```powershell
git clone https://github.com/dmonto/maf-course.git
if ($LASTEXITCODE -ne 0) { throw 'Clone failed.' }
Set-Location ./maf-course/maf-unit07
& ./Scripts/Initialize-Environment.ps1
dotnet restore ./Maf.Unit07.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build ./Maf.Unit07.slnx --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& ./Scripts/Run-Example.ps1 -Example v01 -Demo -NoBuild
```

For an existing clone, use `git pull` at its root and then enter `maf-unit07`. The default mode runs the real MAF workflow engine with a deterministic C# draft. No model or Azure resource is needed for demo execution. The deterministic draft is not an LLM and does not evaluate prompt quality.

All comments, prompts and documentation are in English. The synthetic service catalog retains VPN `healthy` and payroll `degraded`. Baseline approval only approves a proposal; it does not create a ticket or contact another person.

## One shared .env

`Initialize-Environment.ps1` creates `.env` from `.env.example` only when missing. Executables also initialize it on first use. Edit this one file at the `maf-unit07` root; do not copy it into the example folders. Keep the whole package together so executables can locate `Maf.Unit07.slnx` even when launched from another working directory.

```dotenv
MAF_EXECUTION_MODE=demo
AZURE_OPENAI_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT_NAME=YOUR-ASSIGNED-DEPLOYMENT
TENANT_ID=YOUR-TENANT-ID
SUBSCRIPTION_ID=YOUR-SUBSCRIPTION-ID
REQUEST_TIMEOUT_SECONDS=45
MAX_OUTPUT_TOKENS=400
STATUS_DELAY_MS=200
GUIDE_DELAY_MS=350
```

Nonempty process environment values override file values. `-Demo` and `-Live` override `MAF_EXECUTION_MODE`. Values are literal strings, with no shell execution or variable interpolation. Open a fresh terminal or clear stale process variables when changing previously imported settings.

Model package versions match the GitBook listings and Unit 6. `Microsoft.Agents.AI.Workflows` is pinned to `1.24.0`. The SDK accepts compatible later .NET 10 feature bands through `global.json`. Lock files pin transitive package resolution.

## Projects and GitBook sections

| Project | Section | Demonstration |
| --- | --- | --- |
| `v01-task-graphs` | Definition of task graphs | Prints the graph and executes the healthy-service path |
| `v02-conditional-transitions` | Conditional transitions | Runs exclusive healthy/degraded routing with typed contracts |
| `v03-error-handling` | Error handling in workflows | Bounded status-read retries and explicit failure |
| `v04-human-review` | Human-in-the-loop integration | Review port, visible proposal, approval and rejection |
| `v05-intermediate-validations` | Intermediate validations | Valid output, rejected service/case and failure boundary checks |
| `v06-sequential-orchestration` | Sequential orchestration | Checks task completion order including the review round trip |
| `v07-parallel-evidence` | Task parallelization | Compares concurrent evidence reads with sequential reads |
| `v08-workflow-persistence` | Workflow persistence | Checkpoint a review and resume it in a new process |
| `v09-workflow-observability` | Workflow observability | Structured events, correlation, elapsed time and optional live drafting |

The final section, Real-world enterprise use cases, contains a design table and Exercise 2 rather than a separate blue executable example. Its exercise is implemented in `Exercises/ex02-idempotent-escalation`.

## v01: inspect the task graph

```powershell
& ./Scripts/Run-Example.ps1 -Example v01 -Demo -NoBuild
```

Expect a `graph` JSON object, executor events and a final `RESULT` with `stage=information-ready`, `status=healthy`. Follow `intake`, `evidence`, `draft`, `finish-information`. The user's report of a local problem does not replace the catalog status.

## v02: compare the conditional paths

```powershell
& ./Scripts/Run-Example.ps1 -Example v02 -Service vpn -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v02 -Service payroll -Decision yes -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v02 -Service payroll -Decision no -Demo -NoBuild
```

Expect `information-ready`, `escalation-approved` and `escalation-rejected`, respectively. The predicates are complementary application-owned checks. The model never chooses whether review is required.

`-Decision` supplies an explicit synthetic test decision and labels it `explicit-cli-test-decision`. Omit it to type `yes` or `no` at the console. This is a local lab host; it is not a reviewer-authentication implementation.

## v03: exhaust the timeout retries

```powershell
# Direct dotnet invocation is useful when a nonzero exit code is expected.
dotnet run --no-build --project ./v03-error-handling/Unit07.V03.csproj -- run payroll --demo --fail
if ($LASTEXITCODE -eq 0) { throw 'Failure injection unexpectedly succeeded.' }
```

Expect `status-attempt` entries 1, 2 and 3, a failure with `status-timeout`, a nonzero exit code, no review and no `RESULT`. The first two timeouts are retried; the third stops execution. A normal healthy run remains possible:

```powershell
& ./Scripts/Run-Example.ps1 -Example v03 -Service vpn -Demo -NoBuild
```

## v04: review the proposal

```powershell
& ./Scripts/Run-Example.ps1 -Example v04 -Demo -NoBuild
```

Read the proposal and type `yes` or `no`. The host creates a response from the original external request. The reviewed finish executor retrieves the saved proposal from scoped workflow state. Approval yields `escalation-approved`; rejection yields `escalation-rejected`.

The review port can complete once when publishing its request and again when consuming its response. These are two phases of one review, not two reviewer approvals.

## v05: validate boundaries

```powershell
& ./Scripts/Run-Example.ps1 -Example v05 -Service vpn -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v05 -Service payroll -Decision no -Demo -NoBuild

dotnet run --no-build --project ./v05-intermediate-validations/Unit07.V05.csproj -- run unknown --demo
if ($LASTEXITCODE -eq 0) { throw 'Invalid service was accepted.' }

dotnet run --no-build --project ./v05-intermediate-validations/Unit07.V05.csproj -- run vpn --case INVALID --demo
if ($LASTEXITCODE -eq 0) { throw 'Invalid case was accepted.' }
```

Invalid input fails before evidence retrieval or drafting. The baseline also rejects a blank/oversized symptom and an empty/oversized draft. Its draft check validates shape and size, not factual accuracy. Exercise 1's timestamp and freshness gate are deliberately absent from this baseline.

## v06: inspect sequential dependencies

```powershell
& ./Scripts/Run-Example.ps1 -Example v06 -Decision yes -Demo -NoBuild
```

Expect `order-verified` followed by the business result. Completion order is `intake`, `evidence`, `draft`, `human-review`, `human-review`, `finish-reviewed`. The repeated port ID covers the outbound request and inbound response. VPN uses `intake`, `evidence`, `draft`, `finish-information`.

## v07: compare parallel and sequential reads

```powershell
& ./Scripts/Run-Example.ps1 -Example v07 -Demo -NoBuild -StatusDelayMs 300 -GuideDelayMs 500
& ./Scripts/Run-Example.ps1 -Example v07 -Demo -NoBuild -Serial -StatusDelayMs 300 -GuideDelayMs 500
```

Both runs should produce the same business outcome. The `evidence-timing` event should be near the larger read delay in parallel mode and near their sum in serial mode, plus overhead. Exact timing varies with the machine.

As in GitBook, parallelism is inside one evidence executor using `Task.WhenAll`; this package does not substitute graph fan-out/fan-in. Both reads must succeed before drafting can start.

## v08: restore a pending review

```powershell
$state = 'restart-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
& ./Scripts/Run-Example.ps1 -Example v08 -Command pause -Service payroll -State $state -Demo -NoBuild
Get-Content "./data/v08/$state/pending.json"
Get-ChildItem "./data/v08/$state/checkpoints" -Recurse -File

# A new dotnet process reconstructs the same graph.
& ./Scripts/Run-Example.ps1 -Example v08 -Command resume -Decision yes -State $state -Demo -NoBuild
```

Pause returns without prompting and only after the pending request has been checkpointed. Resume re-emits the review, then returns `escalation-approved`. Intake, evidence and draft do not repeat. The bookmark is removed after successful completion.

Each project and `-State` combination gets a separate directory under `data`. State paths resolve from the package root, not the current working directory. Checkpoints are framework JSON files; `pending.json` is only a bookmark and configuration key. Keep the same project, state, provider and package version when resuming.

`-Reset` explicitly deletes only the selected example/state directory before starting again. Use a new `-State` for independent experiments. The lab is single-process and single-operator; it has no distributed lease or tenant authentication.

## v09: events and live drafting

```powershell
& ./Scripts/Run-Example.ps1 -Example v09 -Demo -NoBuild
```

Every workflow event includes session ID, UTC timestamp and elapsed milliseconds. Executor completion events include the executor ID. `RESULT` is emitted only after the stream finishes successfully. Other JSON objects describe configuration, attempts, review, timing or failures.

For live inference, edit the one root `.env` with the assigned Azure values. The endpoint must be the HTTPS Azure OpenAI resource-root URL, not a Foundry project URL or `/openai/v1/` base URL.

```powershell
& ./Scripts/Login-Azure.ps1
& ./Scripts/Run-Example.ps1 -Example v09 -Live -NoBuild

$state = 'live-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
& ./Scripts/Run-Example.ps1 -Example v09 -Command pause -Service payroll -State $state -Live -NoBuild
& ./Scripts/Run-Example.ps1 -Example v09 -Command resume -State $state -Live -NoBuild
```

Live mode calls the assigned deployment and may incur usage. It uses Azure CLI identity, a bounded model deadline and the original drafting instructions. It does not silently fall back to a deterministic answer if Azure fails. The configuration key includes provider and live endpoint/deployment identity.

This is the unit's console event stream; no DevUI host or OpenTelemetry exporter is added. Redirect console output if local logs need to be retained. Review output includes the synthetic proposal text.

## Exercise solutions and verification

See [Exercises/README.md](Exercises/README.md) for the two possible instructor solutions. Neither solution is required by any `v01`–`v09` project.

```powershell
& ./Scripts/Test-Examples.ps1 -NoBuild
```

The smoke test launches independent processes for routes, failures, restart, freshness and post-write recovery. Python is optional and is needed only for the deeper database and event checks:

```powershell
python ./Verification/verify.py
```

The Python verification uses only its standard library and the already-built .NET projects. It checks that the recovered write reuses its original receipt and that SQLite contains one escalation. It writes `Verification/last-run.json`. See [VALIDATION.md](VALIDATION.md) for the validation performed while preparing this package and [GITBOOK-MAPPING.md](GITBOOK-MAPPING.md) for source alignment.
