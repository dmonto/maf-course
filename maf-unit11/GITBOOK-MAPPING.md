# Current GitBook to project mapping

Source page: `session-5/unit-11`, read on 7 November 2026. The complete source snapshot is retained under `Reference/`. The page was not edited for this code delivery.

| Syllabus section | Example in the current page | Executable implementation |
| --- | --- | --- |
| 1. Event-driven architecture | Conceptual envelope and delivery model | Shared `SupportEvent`, `StatusData` and validation; used by every project |
| 2. Integration with queues | Blue example: prepare Unit11.Events; three source listings | `v01-integration-with-queues`; original listings refactored into `Shared` |
| 3. Real-time processing | Reasoners.cs, EventWorker.cs and Program.cs listings | `v02-real-time-streaming`; streamed text and timings; HTTP host reused by v03 |
| 4. Reaction to external events | Blue example: synthetic VPN observation; complete broker listing | `v03-external-events`, `Scripts/Send-Event.ps1`, shared `ServiceBusBridge` exposed by v07 |
| 5. Asynchronous scenarios | Blue example: retry after partial output | `v04-asynchronous-retries` |
| 6. Concurrency management | Blue narrative example: inspect interleaved work | `v05-concurrency` with eight events and a measured maximum |
| 7. Event persistence | Blue narrative example: verify restart behavior | `v06-event-persistence`, launching three real independent processes |
| 8. Streaming observability | Signal table and tracing guidance | Shared scoped JSON logs; v02/v04/v05/v07 timings and terminal states |
| 9. Latency control | Budget and capacity explanation | Common .env budgets; worker deadline, lease, retry policy and metrics |
| 10. Enterprise use cases | Blue example: Azure OpenAI; full broker procedure | `v07-enterprise-events`: `stream`, `serve`, `bus`, `bus-send` and inspection commands |

## Packaging changes

* Source `internal` types are public members of `Maf.Unit11` so independent projects can reuse one compiled library.
* A single dotenv loader replaces repeated shell configuration and supports the established `MAF_EXECUTION_MODE`, `--demo` and `--live` conventions.
* Default state is isolated by project beneath `.data`. The v07 host and adapter deliberately share their v07 inbox.
* Original atomic claims, stable payload hashes, owner fencing, three-attempt limit, source/data bounds, MAF streaming and manual broker settlement are retained.
* Identity checking and insertion use one short SQLite write transaction. Baseline intake has no admission cap.
* Ports and timing values are explicit. Defaults retain the source's two consumers, 500 ms poll, 45-second attempt budget, 90-second lease and 350-token generation bound.
* Fixture output is labelled. Live errors never fall back to a fixture.


The original `Unit11.Events.csproj`, `EventTypes.cs`, `EventStore.cs`, `Reasoners.cs`, `EventWorker.cs`, `Program.cs` and `ServiceBusBridge.cs` are preserved verbatim in `Reference/OriginalListing` for comparison. They are not extra solution projects.
