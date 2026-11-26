# GitBook source map

Page: [Unit 15](https://diego-montoliu.gitbook.io/maf-course/session-7/unit-15).

The matching text is `Documentation/Unit15.md`. Its C# excerpts are extracted from the actual source files; the full runnable projects remain the implementation authority. Every source excerpt is wrapped and expandable in GitBook. The two exercise solutions are isolated under `Exercises`.

| Example | Executable project | Expected behavior |
| --- | --- | --- |
| v01 | `v01-separation-of-responsibilities/Maf.Unit15.V01.csproj` | One assessment, one pending event, zero inbox entries and no review items. No external business actions. |
| v02 | `v02-integration-with-microservices/Maf.Unit15.V02.csproj` | The selected adapter, evidence provenance and accepted assessment. HTTP mode exposes the actual host response. |
| v03 | `v03-event-driven-design/Maf.Unit15.V03.csproj` | First delivery applied, replay duplicate, one inbox entry and one VPN review item. |
| v04 | `v04-applying-clean-architecture/Maf.Unit15.V04.csproj` | An injected failure leaves the event pending after consumer commit. Recovery returns duplicate and clears pending work. |
| v05 | `v05-advanced-dependency-injection/Maf.Unit15.V05.csproj` | Two distinct DI scopes, one evidence read per scope, a reused accepted retry and rejected singleton capture. |
| v06 | `v06-contract-management/Maf.Unit15.V06.csproj` | Four rejected malformed or conflicting events and exactly one accepted inbox entry. |
| v07 | `v07-cross-cutting-observability/Maf.Unit15.V07.csproj` | Retained JSON diagnostics with operation, correlation, event and trace identities. HTTP mode propagates W3C trace context. |
| v08 | `v08-dependency-control/Maf.Unit15.V08.csproj` | No provider references in the compiled core. The explicit healthy email fixture creates no review item and calls no model. |
| v09 | `v09-modularity/Maf.Unit15.V09.csproj` | Approval precedes repair request, repeated command returns duplicate, and verification branches end in Verified or NeedsManualRecovery. |
| v10 | `v10-structural-scalability/Maf.Unit15.V10.csproj` | Review commits independently of an audit outage. Audit replay returns applied, duplicate, duplicate and creates one audit record. |

| Shared concept | Source |
| --- | --- |
| Domain policy | `Shared/Maf.Unit15.Domain/AssessmentPolicy.cs` |
| DTOs, validation and ports | `Shared/Maf.Unit15.Application/Contracts.cs` |
| Assessment and delivery use cases | `Shared/Maf.Unit15.Application/AssessmentService.cs` |
| MAF agent, tool, RunAsync and telemetry | `Shared/Maf.Unit15.Infrastructure/EvidenceReaders.cs` |
| Producer transaction | `Shared/Maf.Unit15.Infrastructure/AssessmentStore.cs` |
| Consumer transaction | `Shared/Maf.Unit15.Infrastructure/Inbox.cs` |
| Durable synthetic process | `Shared/Maf.Unit15.Infrastructure/RepairProcess.cs` |
| One shared environment file | `Shared/Maf.Unit15.Infrastructure/EnvironmentFile.cs` |
| Run context, diagnostics and composition | `Shared/Maf.Unit15.Examples/ExampleRunner.cs` |
| HTTP status and health endpoints | `EvidenceHost/Program.cs` |

The verification report is generated at `artifacts/verification-latest.json`; it distinguishes fixture verification from a separate Azure inference test.
