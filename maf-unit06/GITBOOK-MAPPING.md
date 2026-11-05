# Unit 6 source-to-project mapping

Source: [Unit 6](https://diego-montoliu.gitbook.io/maf-course/session-3/unit-6), read on 2026-11-05.

| Topic position | Exact H2 | Example project | Coverage |
| --- | --- | --- | --- |
| 1 | In-session memory | v01-in-session-memory | Same and fresh sessions |
| 2 | Persistence in external databases | v02-database-persistence | Scoped SQLite snapshot, business state and transactional audit |
| 3 | Long-context management | Shared/Unit06.Common | Limits and separate business state; no separate blue example |
| 4 | Truncation strategies | v03-complete-turn-truncation | Complete function-call/result groups and size ceiling |
| 5 | Contextual enrichment | v04-contextual-enrichment | Full assistant with case and service reads |
| 6 | Use of distributed storage | v05-replica-resume | Narrative converted into a reproducible two-process local simulation |
| 7 | Consistency control | v06-consistency-control | Deliberate stale-revision rejection |
| 8 | Security in persistent memory | Shared/Unit06.Common and Verification | Scoped access, current tool authorization, bounded trusted snapshots and expiry; no separate blue example |
| 9 | Conversation auditing | v07-conversation-auditing | Metadata inspection and process-restart script |
| 10 | Robust state design | Shared/Unit06.Common and Exercises/ex02-configurable-idle-expiry | Compatibility, schema, failure paths and separate exercise solution |

## Explicit package adaptations

- Original application files are preserved under `Reference/GitBook-original`; the current Markdown source is preserved under `Reference/unit-6-source.md`.
- Stable code is namespaced and factored into one shared class library. Each mapped section has its own .csproj and Program.cs.
- The package root `.env` replaces the original PowerShell-only configuration loading. Process variables can still supply the assigned environment.
- Named CLI options replace the original positional arguments. Use Scripts/Run-Example.ps1 and the documented examples rather than passing the old positional command line to the new executables.
- Explicit scripted-demo mode enables deterministic local inspection of real MAF session and tool mechanics; live mode uses the assigned Azure deployment. Providers have different compatibility keys.
- v05 simulates independent replicas on one machine. It does not add a networked database backend absent from the source.
- v06 stages overlapping revisions deterministically rather than relying on nondeterministic terminal timing.
- The repository serializes the runtime business-state type and exposes a lifetime parameter so the exercise-specific domain and validation code can remain inside Exercises. Baseline examples keep the original case fields and fixed 24-hour policy.
- The reducer rejects character budgets below two, the minimum size of its empty JSON array.

No GitBook content or exercise statements were changed by creating this package.
