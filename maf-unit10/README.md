# Unit 10 - REST, databases and Slack (.NET 10)

Nine independent example projects follow the nine sections of the current [GitBook Unit 10](https://app.gitbook.com/s/5NyZDQFujwe6GeJ3CBCA/session-5/unit-10). Shared implementation, agent instructions and one `.env` live at this unit root. The two instructor exercise solutions are separate projects under `Exercises/`. Source comments and instructions are in English.

## Quick start

Place this entire `maf-unit10` directory inside your existing `maf-course` checkout. The archive already contains a common `.env` with synthetic local defaults and empty Azure/Slack settings. Do not create `.env` files in individual projects.

```powershell
# From the maf-course repository root.
Set-Location .\maf-unit10
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
.\Scripts\Run-Example.ps1 -Example v01 -Demo
.\Scripts\Run-Example.ps1 -Example v02 -Demo
.\Scripts\Run-Example.ps1 -Example v09 -Demo -Case acceptance
```

Requirements: the .NET 10 SDK, Git and NuGet access for the first restore. The local demonstration needs no Azure deployment, external database, Slack token or background server. It uses the real MAF function wrappers, the real SQLite provider and the same HTTP adapters as the page, with an in-memory HTTP transport. Demo results explicitly report `modelInvoked=false`; no model selected those calls.

`Run-Example.ps1` builds the selected project. Add `-NoBuild` after a successful build. Every program returns a final `RESULT` JSON line and a nonzero exit code on an unexpected failure. An intentionally rejected API contract or notification can also be demonstrated as an expected test result within a successful scenario.

Build and test the full pack:

```powershell
dotnet build .\Unit10.slnx --maxcpucount:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
.\Scripts\Test-Examples.ps1 -NoBuild
```

You can also invoke a project directly from any working directory. The unit root is located from its executable directory:

```powershell
dotnet run --project .\v01-rest-apis\Unit10.V01.csproj -- --demo
dotnet run --project .\v06-external-data-validation\Unit10.V06.csproj -- --demo --case stale
```

Keep example directories together with `Shared/`, `Prompts/`, `manifest.json`, the build properties and the common configuration.

## Section-to-project mapping

| ID | GitBook section | Directory | Default observation |
| --- | --- | --- | --- |
| v01 | Connection to REST APIs | `v01-rest-apis` | Real function-tool schemas, degraded VPN status and 2 open tickets |
| v02 | Integration with databases | `v02-database-integration` | Training-a has 2 open VPN tickets; training-b has 1 |
| v03 | Integration with Slack | `v03-slack-integration` | Exact notification preview; delivery requires `-Send` |
| v04 | Secure credential management | `v04-credential-management` | Configuration presence without printing values |
| v05 | Access control | `v05-access-control` | Read/write permission matrix and hidden runtime scope |
| v06 | External data validation | `v06-external-data-validation` | Invalid identity, status, age, JSON, size and media type are rejected |
| v07 | External error management | `v07-external-error-management` | Bounded read retry, Slack method errors, rate limit and uncertain outcomes |
| v08 | Orchestration with legacy systems | `v08-legacy-orchestration` | VPN01/D is translated into vpn/degraded before preparing the update |
| v09 | Integration best practices | `v09-integration-best-practices` | Complete preview or seven local acceptance checks |

The conceptual legacy example has executable code in v08 even though its GitBook section contains no full listing. V09 implements the concluding integration walkthrough and verification table. The owner predicate and maintenance-freeze rule are implemented only in the instructor solution projects.

## Useful runs and expected results

Run these commands from `maf-unit10`:

```powershell
.\Scripts\Run-Example.ps1 -Example v03 -Demo
.\Scripts\Run-Example.ps1 -Example v03 -Demo -Send -Approve -Operation local-send-001
# Reusing this ID must return already-sent, with no additional fixture POST.
.\Scripts\Run-Example.ps1 -Example v03 -Demo -Send -Approve -Operation local-send-001
.\Scripts\Run-Example.ps1 -Example v05 -Demo
.\Scripts\Run-Example.ps1 -Example v06 -Demo -Case tenant
.\Scripts\Run-Example.ps1 -Example v07 -Demo
.\Scripts\Run-Example.ps1 -Example v08 -Demo
.\Scripts\Run-Example.ps1 -Example v09 -Demo -Case acceptance
.\Scripts\Run-Example.ps1 -Example v09 -Demo -Agent
.\Scripts\Run-Example.ps1 -Example ex01 -Demo
.\Scripts\Run-Example.ps1 -Example ex02 -Demo
.\Scripts\Run-Example.ps1 -Example verify -Demo
```

`-Approve` is a local-demo convenience for automated testing and requires `-Send`. It is rejected together with `-Live`. Without it, `-Send` asks for the exact phrase `SEND <operation-id>` after displaying the destination and payload. Without `-Send`, v03, v08 and the v09 walkthrough prepare a preview and perform no delivery attempt.

V06 supports `-Case all`, `stale`, `future`, `invalid`, `tenant`, `service`, `missing`, `malformed`, `oversized` and `media`. Its free-text-injection check also proves that external summaries are excluded from notifications. V07 runs its complete local error matrix; it performs no automatic POST retry. V09 `-Case acceptance` generates `reports/integration-acceptance.json`.

An expected permission denial can be checked directly:

```powershell
dotnet run --project .\v03-slack-integration\Unit10.V03.csproj -- --demo --profile employee
# Expected: error / UnauthorizedAccessException and exit code 1.
```

## One common .env and Azure execution

Edit only `maf-unit10/.env`. The loader supports simple `KEY=value` assignments and matching single/double quotes. It does not execute scripts or expand expressions. Nonempty process environment variables take precedence, so existing `student.env.ps1` settings can also be used. `.env` is ignored by Git; the archive's copy contains only sample defaults.

For model inference, fill the assigned `TENANT_ID`, `SUBSCRIPTION_ID`, `AZURE_OPENAI_ENDPOINT` and `AZURE_OPENAI_DEPLOYMENT_NAME`. Use the resource-root Azure OpenAI endpoint. The deployment must support function tools and the Azure CLI user must have the assigned model-inference permission, such as Cognitive Services OpenAI User.

Start the real HTTP fixture in terminal A:

```powershell
.\Scripts\Start-Fixture.ps1
# Keep this terminal running at http://127.0.0.1:5098.
```

In terminal B:

```powershell
. .\Scripts\Import-Env.ps1
az login --tenant $env:TENANT_ID
if ($LASTEXITCODE -ne 0) { throw 'Azure login failed.' }
az account set --subscription $env:SUBSCRIPTION_ID
if ($LASTEXITCODE -ne 0) { throw 'Subscription selection failed.' }
.\Scripts\Run-Example.ps1 -Example v01 -Live
.\Scripts\Run-Example.ps1 -Example v09 -Live -Agent
```

V01 in live mode and v09 with `-Agent -Live` use `AzureOpenAIClient`, `AzureCliCredential`, `ChatClientAgentOptions`, `AsAIAgent` and the two registered read tools. All ordinary SQL fixtures remain local. V01 demo mode invokes those `AIFunction` wrappers explicitly; it does not simulate a model answer.

To use actual HTTP and SQLite without model inference:

```powershell
.\Scripts\Run-Example.ps1 -Example v03 -Live
.\Scripts\Run-Example.ps1 -Example v08 -Live
```

The HTTP fixture also exposes the legacy status route required by v08. Stop it with Ctrl+C to select a fault:

```powershell
.\Scripts\Start-Fixture.ps1 -StatusFault stale
# In another terminal: ./Scripts/Run-Example.ps1 -Example v06 -Live
```

The fixture supports status faults `none`, `stale`, `invalid`, `tenant`, `forbidden`, `unavailable`, and Slack faults `none`, `scope`, `rate`. A configured corporate API must implement the page's status contract. The added legacy status route is documented in `v08-legacy-orchestration/README.md`.

## A reviewed send to the assigned training Slack channel

Keep the status API available. Set `SLACK_MODE=live` and the assigned `SLACK_CHANNEL_ID` in the common environment. Install a bot with `chat:write` and invite it to that training channel. Load the token only into the client process through a hidden prompt:

```powershell
$env:SLACK_MODE = 'live'
$env:SLACK_CHANNEL_ID = Read-Host 'Assigned training Slack channel ID'
$secureToken = Read-Host 'Assigned training bot token' -AsSecureString
$tokenPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
try {
    $env:SLACK_BOT_TOKEN = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPointer)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPointer)
    $secureToken.Dispose()
}
try {
    .\Scripts\Run-Example.ps1 -Example v03 -Live -Send -Operation training-slack-001
    # Review LIVE training Slack, channel and text, then type SEND training-slack-001.
} finally {
    $env:SLACK_MODE = 'fixture'
    Remove-Item Env:SLACK_BOT_TOKEN -ErrorAction SilentlyContinue
}
```

The MAF tools are read-only. The host owns the immutable notification, the approval, the two-minute review expiry and the ledger claim. HTTP 200 with `ok:false` is not a successful delivery. A success requires the configured channel and a nonempty Slack receipt.

## Persistence

Each project stores its synthetic database under `data/<example>/<synthetic-subject>/unit10.db`. Files are located relative to the unit root, not the caller's current directory. A repeated operation ID in the same database is blocked even if its proposed text changes. `sending`, `sent`, `rejected` and `unknown` are durable states. An uncertain outcome must be reconciled before making another operational attempt. This is local duplicate suppression, not an atomic database-plus-Slack transaction.

Use a new ID only for a separately reviewed operation. The archive excludes generated databases, build outputs and reports. Fresh extraction therefore starts with empty delivery ledgers.

## Instructor solutions

- `Exercises/ex01-owner-scoped-tickets` adds ownership to its own synthetic database, registers `GetMyOpenTicketIds`, returns at most five IDs, preserves tenant/service/owner/open-status predicates and leaves the aggregate count unchanged. The model can supply only `service`. An employee receives A-101, not another owner's ticket or the training-b ticket.
- `Exercises/ex02-legacy-maintenance-freeze` adds an isolated maintenance adapter and its own synthetic HTTP response fixtures. `Y` blocks before review or claim; `N` preserves the reviewed send; unknown flags and mismatched services fail validation. It also repeats a completed operation ID. It runs only with the local demo and makes no real Slack request.

`Exercises/` contains complete instructor solutions. No GitBook page was edited as part of generating this pack.

## Source alignment and verification

`Reference/unit10-gitbook.md` records the page retrieved on 7 November 2026. `Reference/Original/` preserves the six original listings as text, and `source.json` records provenance and a page hash. `Shared/Contracts.cs` and `Shared/Connectors.cs` match the retrieved listings byte for byte. `Shared/Stores.cs` keeps the original SQL queries and ledger; its seed INSERT explicitly names the four baseline columns so that rerunning Exercise 1 also works after adding ownership. `Prompts/integration.txt` retains the page's agent instructions. The fixture adds a legacy status endpoint; the shared host adds root `.env` loading, per-project storage and reusable notification orchestration.

The pinned packages remain Azure.AI.OpenAI 2.9.0-beta.1, Azure.Identity 1.21.0, Microsoft.Agents.AI.OpenAI 1.24.0, Microsoft.Data.Sqlite 10.0.10 and SQLitePCLRaw.lib.e_sqlite3 3.53.3. Lock files are provided; subsequent restores can use `dotnet restore .\Unit10.slnx --locked-mode --disable-parallel`.

Local verification covers all nine examples, both solutions, real MAF function invocation, real SQLite behavior, HTTP request/response contracts, permission denials, stale and malformed data, rate limits, uncertain outcomes, expiry, duplicate suppression and concurrent claims. Live Azure inference and real Slack delivery require the assigned external resources and were not executed with production credentials.
