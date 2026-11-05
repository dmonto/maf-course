# MAF Unit 6: executable examples

Memory, State and Long Conversations: Persistent Agents and Durable Contexts in MAF.

Seven projects correspond to the seven GitBook sections containing examples, including the narrative examples about replicas and conflicting requests. Each has its own executable entry point. Stable implementation code is shared under `Shared/Unit06.Common`, with one `.env` at this package root. Possible exercise solutions are isolated in `Exercises`.

## Prerequisites and installation

Use the .NET 10 SDK, PowerShell and Git. Live inference additionally requires Azure CLI and an assigned Azure OpenAI deployment supporting function tools through Chat Completions.

```powershell
git clone https://github.com/dmonto/maf-course.git
if ($LASTEXITCODE -ne 0) { throw 'Clone failed.' }
Set-Location ./maf-course/maf-unit06
```

If the repository is already cloned, run `git pull` in its root and enter `maf-unit06`.

```powershell
& ./Scripts/Initialize-Environment.ps1
dotnet restore ./Maf.Unit06.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build ./Maf.Unit06.slnx --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& ./Scripts/Run-Example.ps1 -Example v04 -Demo -NoBuild
```

Expect `outcome=committed`, `execution=demo-scripted-model`, a `GetCaseContext` call and the synthetic case `INC-DEMO-001` at stage `investigating`. All code comments, prompts and documentation are in English.

The initializer creates `.env` from `.env.example` only when missing. The executables do the same on first use. Edit that single root file; do not copy it into each project. The generated `.env`, SQLite files and build output are excluded from Git. Package-root discovery works from an example directory or from its executable directory by locating `Maf.Unit06.slnx`.

## Example map

| ID | GitBook section | Executable behavior |
| --- | --- | --- |
| v01 | In-session memory | Same-session recall and comparison with a fresh session; no database |
| v02 | Persistence in external databases | One SQLite-backed turn; restore across process restarts |
| v03 | Truncation strategies | Six tool-bearing turns reduced to four; call/result pairs remain complete |
| v04 | Contextual enrichment | Complete persistent assistant with case and fresh-status tools |
| v05 | Use of distributed storage | Replica A and B run as independent processes against the same local store |
| v06 | Consistency control | Both candidates load revision N; the stale candidate is rejected after the winner commits |
| v07 | Conversation auditing | Latest ten committed-turn metadata rows; optional audit-only mode |

Long-context management, Security in persistent memory and Robust state design contain no separate blue example in the current source. Their policies are implemented in the shared code; Exercise 2 belongs to Robust state design. See [GITBOOK-MAPPING.md](GITBOOK-MAPPING.md).

## Demo and live modes

The default `.env` selects `demo`. The deterministic `ScriptedMemoryClient` runs inside the real MAF session, history, function-calling and serialization pipeline. Its responses are explicitly labelled. It is not an LLM, does not establish prompt quality and is never used as a fallback for a failed live request.

For live inference, edit the shared `.env`:

```dotenv
MAF_EXECUTION_MODE=live
AZURE_OPENAI_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT_NAME=YOUR-ASSIGNED-DEPLOYMENT
TENANT_ID=YOUR-TENANT-ID
SUBSCRIPTION_ID=YOUR-SUBSCRIPTION-ID
```

Use the HTTPS resource-root endpoint for `AzureOpenAIClient`, rather than a Foundry project URL or an `/openai/v1/` base URL.

```powershell
& ./Scripts/Login-Azure.ps1
& ./Scripts/Run-Example.ps1 -Example v04 -Live -NoBuild -Session live-case-001
```

`-Demo` and `-Live` explicitly override the file's execution mode. Nonempty process environment variables take precedence over `.env` values. The dotenv reader treats values as literal strings; it performs no shell execution or interpolation. Clear stale process variables or open a new terminal after changing values previously imported into the terminal.

Demo and live providers have different compatibility keys. Default session IDs include the mode. When choosing your own session ID, use a new ID if switching providers; a mismatched stored configuration fails with `config-mismatch`.

v03 always performs local session/history operations without inference. `-Audit` on v02, v04, v07 and the exercise projects reads local metadata without inference. Live requests in other paths use Azure and can incur model usage.

## v01: in-session memory

```powershell
& ./Scripts/Run-Example.ps1 -Example v01 -Demo -NoBuild
```

Expect Windows 11, error 809 and marker `copper-51` in `sameSessionAnswer`. `freshSessionAnswer` asks for missing information. No SQLite row is written. Replace `-Demo` with `-Live` to observe the assigned model's behavior.

## v02: persistence across process restarts

```powershell
$session = 'persist-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
& ./Scripts/Run-Example.ps1 -Example v02 -Demo -NoBuild -Session $session `
    -InputText 'My test marker is copper-51. I use Windows 11 and VPN error 809.'
& ./Scripts/Run-Example.ps1 -Example v02 -Demo -NoBuild -Session $session `
    -InputText 'What test marker, operating system and VPN error did I report?'
```

The first process reports `restored=false`, revision 1. The next process reports `restored=true`, revision 2 and the retained facts. The absolute database path is resolved from `MAF_MEMORY_DATABASE` against the package root, so changing the current directory does not silently create a second database.

## v03: complete-turn truncation

```powershell
& ./Scripts/Run-Example.ps1 -Example v03 -Demo -NoBuild
```

Expect six original turns, four kept turns, two dropped turns and sixteen retained messages. `functionCallIds` and `functionResultIds` match. A second reduction with a deliberately tiny ceiling removes whole turns. The size ceiling counts serialized characters, not tokens.

## v04: contextual enrichment and current evidence

```powershell
$session = 'context-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
& ./Scripts/Run-Example.ps1 -Example v04 -Demo -NoBuild -Session $session
& ./Scripts/Run-Example.ps1 -Example v04 -Demo -NoBuild -Session $session `
    -InputText 'Is the VPN service currently healthy?'
& ./Scripts/Run-Example.ps1 -Example v04 -Demo -NoBuild -Session $session `
    -InputText 'What is the current status of payroll?'
```

Expect `GetCaseContext` for the case request and a fresh `GetServiceStatus` call for each status question. The synthetic catalog returns VPN `healthy` and payroll `degraded`. These are controlled records, not real infrastructure checks. Tool names executed in the run appear in `toolCalls`; retained tool results are part of the framework session snapshot.

The model receives no tool for changing the case stage. Business state is application-owned. The baseline does not implement the confirmed-operating-system extension; its solution is in `Exercises/ex01-confirmed-operating-system`.

## v05: independent replica processes

Run these commands in the same package, or one in each of two PowerShell windows:

```powershell
$session = 'replica-demo-001'
& ./Scripts/Run-Example.ps1 -Example v05 -Demo -NoBuild -Session $session -Replica A
& ./Scripts/Run-Example.ps1 -Example v05 -Demo -NoBuild -Session $session -Replica B
```

Replica A commits a turn. Replica B starts another process, reconstructs an independent agent/session and recalls the earlier details from the shared file. Repeat with a fresh session ID when comparing demo and live.

This project is labelled `single-machine-replica-simulation`. SQLite remains local to one machine. The project demonstrates the recovery contract in the GitBook narrative; it does not deploy SQL, Cosmos DB, Redis or Blob Storage. A multi-host application requires a genuine shared backend, ownership checks and conditional writes. Copying this database into each container would create independent stores.

## v06: deliberately conflicting candidates

```powershell
& ./Scripts/Run-Example.ps1 -Example v06 -Demo -NoBuild
```

The project stages two independent candidate sessions from exactly the same stored revision. It commits candidate A, then attempts B with the old expected revision. Expect `rejectedCandidate=version-conflict`, `winnerPreserved=true` and an audit row only for the winner. No timing race or repeated terminal clicking is required to reproduce the rejection.

The demonstration catches its expected conflict and exits successfully. An uncaught conflict during an ordinary v02/v04/v05 turn exits with code 3. The read tools have no external side effects; a real write capability would also need business idempotency.

## v07: conversation audit

```powershell
& ./Scripts/Run-Example.ps1 -Example v07 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v07 -Demo -NoBuild -Session $session -Audit
```

The default creates a fresh conversation with two committed turns and reads the audit. For `-Audit`, supply the ID of an existing conversation from another example. An unknown ID returns an empty audit rather than creating a conversation. Rows are returned newest revision first, limited to ten.

The audit contains run IDs, revisions, timestamps, input/output character counts, dropped turns and measured elapsed time. It is not a full transcript or a token/cost report. The snapshot and successful-turn audit row commit together; failed candidates do not add a successful row.

## Verification commands

```powershell
& ./Scripts/Test-Examples.ps1 -NoBuild
& ./Scripts/Test-Memory.ps1
```

The first script executes all seven profiles and both exercise solutions in explicit demo mode. The second launches a new process for every turn and checks restart recall, separation between conversation IDs, bounded history and seven committed-turn rows. For real-model checks:

```powershell
& ./Scripts/Test-Memory.ps1 -Live
```

See [VALIDATION.md](VALIDATION.md) for checks actually performed when creating this package. See `Verification` for repository-level boundary tests. The PowerShell script checks conversation IDs under the fixed synthetic actor; the additional storage tests check different tenant and owner identities.

## Configuration and storage contract

The defaults match the topic: four complete saved turns, 16,000 serialized-history characters, a 128,000-character snapshot ceiling, a 1,000-character current input, 800 output tokens per model call, three function-invocation iterations and a 45-second cooperative run cancellation budget. Three iterations are not a three-tool-call count.

Baseline memory has a sliding 24-hour idle lifetime. Expired snapshots fail with `memory-expired`; expiration does not physically delete rows or backups. The SQLite file contains synthetic conversation content and is not encrypted by this application. Host identity and current tool authorization are resolved outside model arguments.

Keep the original snapshot under `Reference` for comparison. It is a read-only copy of the GitBook source and its original monolithic project, not another independently configured example. The executable examples use shared files and the common `.env`; they do not rely on `student.env.ps1`.
