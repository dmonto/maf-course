# MAF Unit 11: executable examples

Events and Real Time: Agents Connected to Queues, Message Buses and Streaming.

Seven independent .NET projects cover every section containing an explicit example or an executable implementation in the current GitBook unit. Stable code lives under `Shared/Unit11.Common`; every project reads the same root `.env`. 

Source: [Unit 11 in GitBook](https://app.gitbook.com/s/5NyZDQFujwe6GeJ3CBCA/session-5/unit-11), read on 7 November 2026. The source snapshot and its seven original listings are in `Reference/`. GitBook was read without modification.

## Install and build

Use the established .NET 10 SDK, PowerShell and Git setup. Extract the ZIP into your existing `maf-course` checkout; it contains one top-level `maf-unit11` directory. Preserve the complete directory, including `Shared`, the solution and configuration files.

From the repository root:

```powershell
Set-Location .\maf-unit11
.\Scripts\Initialize-Environment.ps1
dotnet restore .\Maf.Unit11.slnx --locked-mode -m:1
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build .\Maf.Unit11.slnx --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
.\Scripts\Run-Example.ps1 -Example v02 -Demo -NoBuild
```

The initializer preserves an existing `.env`. The shipped `.env` and `.env.example` contain only matching placeholders and local defaults. Edit this one `.env`; do not copy it into the example directories. Every executable can find the unit root from its own executable directory, including when launched from another working directory. Compatible later .NET 10 SDK feature bands are accepted by `global.json`.

## Projects and expected results

| ID | GitBook section | Directory | Expected result |
| --- | --- | --- | --- |
| v01 | Integration with queues | `v01-integration-with-queues` | One durable event; matching duplicate adds no work; changed payload rejected; atomic claim completed |
| v02 | Real-time processing | `v02-real-time-streaming` | Provisional text fragments followed by one final `RESULT`; first-text and processing timings |
| v03 | Reaction to external events | `v03-external-events` | HTTP 202 after persistence; job progresses to `Completed`; saved-envelope retry is harmless |
| v04 | Asynchronous scenarios | `v04-asynchronous-retries` | Interrupted first attempt retries successfully on attempt 2; poison event reaches `Dead` on attempt 1 |
| v05 | Concurrency management | `v05-concurrency` | Eight isolated events; no more than two active attempts with default settings |
| v06 | Event persistence | `v06-event-persistence` | Three independent processes seed, process and verify the same database and envelope |
| v07 | Enterprise use cases | `v07-enterprise-events` | Durable event-to-explanation path, optional Azure MAF streaming and Service Bus bridge |

Six sections contain blue example headings. Real-time processing also contains executable agent/worker/host code, so it receives its own project. The narrative concurrency and restart examples have complete runnable projects despite containing no code blocks in GitBook. Conceptual sections 1, 8 and 9 are covered by the shared envelope, operational logs and timing configuration. See `GITBOOK-MAPPING.md` for the complete ten-section mapping.

All one-shot projects finish with a `RESULT` JSON line and return a nonzero exit code on an unexpected failure. v03 defaults to a long-running HTTP server; stop it with Ctrl+C. Progress text is provisional until the terminal result has been committed.

## One shared .env and execution modes

`-Live` or `--live` selects the Azure-backed `MafReasoner` in v02, v03 and v07. It uses the pinned MAF `AIAgent`, `AsAIAgent` and `RunStreamingAsync` APIs from the source unit. A failed live run remains failed; there is no automatic switch to fixture text. v01, v04, v05, v06  require demo mode because they demonstrate deterministic queue or failure behavior.

Nonempty process environment variables take precedence over the file. The CLI `-Demo` and `-Live` switches override the execution mode. Values are literal assignments; the loader never evaluates shell code, expands variables or prints credentials. Open a fresh terminal or clear an existing process variable when intentionally replacing a value already imported into the shell.

| Setting | Default | Meaning |
| --- | --- | --- |
| `MAF_EXECUTION_MODE` | `demo` | Fixture or Azure-backed generation |
| `WORKER_COUNT` | `2` | Local concurrent attempts per process |
| `POLL_INTERVAL_MS` | `500` | Polling interval while no ready work exists |
| `REQUEST_TIMEOUT_SECONDS` | `45` | Cooperative attempt cancellation budget |
| `CLAIM_LEASE_SECONDS` | `90` | Recoverable ownership lease; must exceed the attempt budget |
| `MAX_OUTPUT_TOKENS` | `350` | Requested Azure output bound |
| `MAX_RESULT_CHARS` | `4000` | Application result bound |
| `FRAGMENT_DELAY_MS` | `150` | Delay between fixture fragments |
| `LAB_FAIL_FIRST` | `0` | Optional first-attempt fault in fixture-backed v02/v03/v07 |
| `LAB_POISON_ID` | empty | Optional fixture event ID that fails permanently |
| `UNIT11_DB` | empty | Empty selects `.data/<example>/queue.db`; an override must be absolute |

Default data files are separate per project. To share a queue deliberately between the v07 HTTP worker and broker adapter, use their common v07 default or the same absolute `UNIT11_DB`. SQLite and the adapter form a single-machine demonstration; a distributed deployment needs a properly shared inbox store.

## Run the queue, retry, concurrency and restart examples

```powershell
.\Scripts\Run-Example.ps1 -Example v01 -Demo -NoBuild
.\Scripts\Run-Example.ps1 -Example v04 -Demo -NoBuild
.\Scripts\Run-Example.ps1 -Example v05 -Demo -NoBuild -Count 8 -DelayMs 150
.\Scripts\Run-Example.ps1 -Example v06 -Demo -NoBuild
```

v04 sets its controlled failure plan explicitly, independently of the optional environment fault switches. The retry result contains only the successful attempt's text. v05 logs each event under a separate scope and reports `maximumConcurrentAttempts`. v06 actually launches three child processes; its final JSON records their process IDs, database path and saved envelope. It reuses that exact envelope for duplicate verification.

To run a project directly:

```powershell
dotnet run --project .\v05-concurrency\Unit11.V05.csproj -- --demo --count 8 --delay-ms 150
```

## Run the HTTP event example

Terminal 1, from the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v03 -Demo -NoBuild
```

Terminal 2, from the same unit root:

```powershell
.\Scripts\Send-Event.ps1 -SaveAs .\.data\vpn-event.json -Wait
.\Scripts\Send-Event.ps1 -File .\.data\vpn-event.json -Wait
Invoke-RestMethod 'http://127.0.0.1:5111/jobs'
```

The first call reports `created: true` and then a completed explanation. The second uses the exact saved envelope and reports `created: false`, with the same attempt count and result. A new observation requires a new event ID; `Send-Event.ps1` generates one unless `-Id` is supplied. Changing the payload of an existing event ID is rejected with HTTP 400.

`Send-Event.ps1` writes UTF-8 without a BOM. `-File` reuses a saved envelope. `-CreateOnly` writes or reads the file without submitting it. `-AgeMinutes`, `-Tenant` and `-EventType` support boundary demonstrations. The local endpoint listens on loopback, with an 8 KiB request-body bound.

Inspect the local database without starting a worker:

```powershell
.\Scripts\Run-Example.ps1 -Example v03 -Demo -Command status -NoBuild
```

`GET /health` describes the running host. `GET /jobs/{id}` inspects one job. `GET /jobs` is capped at 100 rows in acceptance order and is not a full backlog metric.

## Use Azure MAF streaming

Set the assigned resource-root endpoint and deployment in the common `.env`:

```dotenv
TENANT_ID=YOUR-TENANT-ID
SUBSCRIPTION_ID=YOUR-SUBSCRIPTION-ID
AZURE_OPENAI_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT_NAME=YOUR-ASSIGNED-DEPLOYMENT
```

```powershell
.\Scripts\Login-Azure.ps1
.\Scripts\Run-Example.ps1 -Example v02 -Live -NoBuild
.\Scripts\Run-Example.ps1 -Example v07 -Live -NoBuild
```

v02 emits text fragments from the live MAF stream. v07 persists the event, processes it through the worker and commits the explanation. No agent has a notification or repair tool. Generated text must not claim that an external action was executed.

For a live HTTP worker, stop v03 and run v07 with its default port 5117:

```powershell
.\Scripts\Run-Example.ps1 -Example v07 -Live -Command serve -NoBuild
```

Submit from another terminal with `Send-Event.ps1 -Port 5117 -Wait`.

## Optional Service Bus path

Use an assigned, existing training namespace and non-session queue. The sender requires Azure Service Bus Data Sender; the bridge requires Azure Service Bus Data Receiver at the relevant scope. Load `SERVICEBUS_NAMESPACE` and `SERVICEBUS_QUEUE` in the same common `.env`. The bridge uses AMQP over WebSockets and Azure CLI authentication.

Keep the v07 HTTP/worker process running in Terminal 1. Terminal 2 starts the receiver:

```powershell
.\Scripts\Run-Example.ps1 -Example v07 -Live -Command bus -NoBuild
```

Terminal 3 creates a fresh envelope and sends it:

```powershell
.\Scripts\Send-Event.ps1 -CreateOnly -SaveAs .\.data\broker-event.json
.\Scripts\Run-Example.ps1 -Example v07 -Live -Command bus-send -File .\.data\broker-event.json -NoBuild
Invoke-RestMethod 'http://127.0.0.1:5117/jobs'
```

The sender confirms broker acceptance; the bridge confirms inbox persistence before broker completion; the worker records the generation outcome separately. A broker failure is never replaced by a local simulation. Matching redeliveries are deduplicated by the inbox even if broker duplicate detection is unavailable or its window has expired.

## Reproduce local verification

```powershell
.\Scripts\Test-Examples.ps1
```

The recorded validation in `VALIDATION.md` covers local execution. Azure model inference and a real Service Bus namespace require the assigned environment and are not part of the offline checks.
