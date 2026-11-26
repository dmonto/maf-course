# Unit 15 — Agentic architectural patterns

Ten independent C# console projects share a .NET 10 domain, application and infrastructure implementation. Each project demonstrates the matching GitBook section. The optional ASP.NET Core evidence host exposes the Unit 14 HTTP contract. All examples and host processes use the single root `.env`.

## Setup

Prerequisites: .NET 10 SDK. The command wrappers support Windows PowerShell 5.1 and PowerShell 7. Azure CLI and the assigned Azure OpenAI resource are needed only for Azure mode.

From the `maf-course` checkout:

```powershell
Set-Location .\maf-unit15
.\Scripts\Initialize-Environment.ps1
dotnet restore .\Maf.Unit15.slnx --locked-mode -m:1
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build .\Maf.Unit15.slnx --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
.\Scripts\Run-Example.ps1 -Example v04 -Mode fixture -NoBuild
.\Scripts\Test-All.ps1 -NoBuild
```

Initialization copies `.env.example` only if `.env` does not exist. The loaders parse literal assignments without executing the file. Existing nonempty process variables take precedence. The projects also load `.env` when launched with `dotnet` directly; no PowerShell wrapper is required.

## Projects

| ID | Directory | Observable result |
| --- | --- | --- |
| v01 | `v01-separation-of-responsibilities` | Assessment and pending outbox entry; no review effect |
| v02 | `v02-integration-with-microservices` | Replaceable fixture, HTTP or MAF evidence adapter |
| v03 | `v03-event-driven-design` | First delivery applied; replay duplicate |
| v04 | `v04-applying-clean-architecture` | Recover after consumer commit and before sender acknowledgement |
| v05 | `v05-advanced-dependency-injection` | Real DI scopes, command retry and rejected singleton capture |
| v06 | `v06-contract-management` | Four malformed or conflicting contracts rejected |
| v07 | `v07-cross-cutting-observability` | Activity spans, business identities and W3C HTTP trace propagation |
| v08 | `v08-dependency-control` | Compiled core has no provider references; healthy fixture path |
| v09 | `v09-modularity` | Durable synthetic transitions, retry and manual recovery |
| v10 | `v10-structural-scalability` | Independent audit subscriber outage and replay |

Each directory has `Program.cs`, its own `.csproj`, a lock file and a README. `examples.json` maps IDs to project paths. Complete source is runnable; `Documentation/Unit15.md` contains the corresponding GitBook text and selected source excerpts.

## Evidence modes

```powershell
# In-process deterministic evidence; no Azure access.
.\Scripts\Run-Example.ps1 -Example v04 -Mode fixture -NoBuild

# Terminal 1: local HTTP host with deterministic evidence.
.\Scripts\Start-EvidenceHost.ps1 -Mode fixture -NoBuild

# Terminal 2, also in maf-unit15: same application through HTTP.
.\Scripts\Run-Example.ps1 -Example v04 -Mode http -NoBuild
```

The default HTTP address is `http://127.0.0.1:8082`. Port 8080 is also accepted for the Unit 14 fixture container. Set `UNIT15_EVIDENCE_BASE_URL` in `.env`, or pass `-BaseUrl`. For a bundled host on port 8080, also set `ASPNETCORE_URLS` accordingly. The host has `/api/status`, `/health/live` and `/health/ready`.

For Azure, configure `AZURE_OPENAI_ENDPOINT` with the HTTPS resource-root URL and `AZURE_OPENAI_DEPLOYMENT_NAME` in `.env`. With `Runner__Identity=cli`, authenticate the assigned CLI session using the established `student.env.ps1` setup. The adapter also supports `managed` and `workload` identities in their configured Azure environments.

```powershell
# In-process MAF adapter; -Live is shorthand for -Mode azure.
.\Scripts\Run-Example.ps1 -Example v04 -Mode azure -NoBuild

# Or stop the fixture host, then run an Azure-backed evidence service.
.\Scripts\Start-EvidenceHost.ps1 -Mode azure -NoBuild
# In another terminal:
.\Scripts\Run-Example.ps1 -Example v04 -Mode http -NoBuild
```

The MAF adapter creates a request-bound tool and agent, registers the tool with `AIFunctionFactory.Create`, invokes `RunAsync`, and checks actual tool execution and exact catalog equality. Tool data remains synthetic in Azure mode. Readiness does not invoke the model. An accepted assessment records `evidence_mode`; `http` identifies transport, not evidence provenance.

Version v08 deliberately forces the healthy email fixture and never calls the model.

## State and recovery

Each default run creates a fresh `artifacts/<version>/<run-id>` directory containing `assessments.db`, `reviews.db`, `result.json` and `events.jsonl`. Version v09 also creates `repair-process.db`; v10 creates `audit.db`. The final stdout line starts with `RESULT ` and includes `status`, `stateDirectory` and the demonstrated state.

`-State` selects an explicit directory and `-OperationId` selects a stable logical command identity. Use a fresh directory for the demonstrations' expected counts. Reusing a state directory intentionally reuses its database contents, so a demonstration that asserts fresh-state counts can fail after a successful previous run. The shared store/dispatcher APIs support recovery against the retained files.

Existing assessment, outbox, inbox, review, audit and synthetic process schemas are retained. Old JSON property names are preserved, including `operation_id` and `evidence_mode`. Pending outbox delivery uses the original payload bytes, so a legacy inbox recognizes its duplicate. Command receipts compare parsed JSON content for compatibility across formatting differences.

The producer commits assessment and outbox together. The consumer commits inbox and its local business effect together. These examples call local consumer adapters directly; a deployed broker, safe multi-worker claims, retention and external-effect idempotency require their own operational implementation.

## Shared code

| Project | Responsibility | Dependencies |
| --- | --- | --- |
| `Shared/Maf.Unit15.Domain` | Deterministic decision policy | .NET only |
| `Shared/Maf.Unit15.Application` | Records, contracts, ports, assessment and dispatch | Domain |
| `Shared/Maf.Unit15.Infrastructure` | MAF/HTTP/fixture evidence, SQLite, process transitions, environment loading | Application, provider packages |
| `Shared/Maf.Unit15.Examples` | Common run context, composition, diagnostics and retained output | Application and Infrastructure |
| `EvidenceHost` | Bounded ASP.NET Core evidence endpoint | Infrastructure |
| `Verification` | Behavioral checks against built console applications and HTTP host | Shared support and isolated reporting solution |

The common build configuration targets `net10.0`. Provider package versions are pinned in the infrastructure project and every project has `packages.lock.json`.

## Possible exercise solutions

`Exercises/Ex01.TransactionBoundaries` demonstrates the broken and correct producer/consumer transaction boundaries. `Exercises/Ex02.ReportingConsumer` owns its reporting database, atomic inbox/counters and quarantine for unknown contracts. They are separate from the ten student examples.

```powershell
.\Scripts\Run-Exercise.ps1 -Exercise 01 -NoBuild
.\Scripts\Run-Exercise.ps1 -Exercise 02 -NoBuild
```

## Direct .NET commands

These commands also work in Bash, replacing the path separators when necessary:

```powershell
dotnet run --project .\v04-applying-clean-architecture\Maf.Unit15.V04.csproj --no-build -- --mode fixture
dotnet run --project .\EvidenceHost\Maf.Unit15.EvidenceHost.csproj --no-build -- --mode fixture
dotnet run --project .\Verification\Verification.csproj --no-build
```

## Verification

`Scripts/Test-All.ps1` builds the solution unless `-NoBuild` is supplied. The .NET verifier then executes the ten fixture projects and both solutions, tests atomic consumer rollback and legacy SQLite replay, starts its own fixture host on an available local port, and checks v02, v04 and v07 over HTTP. It checks shared trace identity in the bundled host and terminates only its own process.

The report is `artifacts/verification-latest.json`. Fixture verification does not test Azure inference. A live Azure check requires the assigned deployment and identity.

## Migration

All runnable Unit 15 examples, exercise solutions and verification now use C#/.NET. The common `.env` and existing `artifacts` are retained during migration. Previous source is backed up outside the active checkout. No other course unit is changed.
