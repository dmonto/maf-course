# Unit 3 source mapping

Source: [Agent Fundamentals: Agents, Tools, Context and Lifecycle](https://app.gitbook.com/s/5NyZDQFujwe6GeJ3CBCA/session-1/unit-3). Read from the current GitBook page on 2026-10-30. The page is the authority for this pack; historical Python content was removed from that page and is not used here.

| GitBook section | Project | Blue example covered |
| --- | --- | --- |
| Formal definition of an agent | v01-bounded-capability | Define a bounded capability |
| Tool registration | v02-tool-registration | Separate schema from enforcement |
| Conversational context management | v03-conversational-context | Follow-up context and fresh evidence |
| Agent lifecycle | v04-agent-lifecycle | Conversation reset |
| Dependency injection | v05-dependency-injection | Complete conversational service-status agent |
| Result management | v06-result-management | A completed refusal |
| User interaction | v07-user-interaction | Ambiguity after two lookups |
| Error handling | v08-error-handling | Diagnose a failure before changing the prompt |

## Sections without a standalone blue example

- Execution control: implemented in `Shared/Unit03.Common/AgentHost.cs` and `CatalogTools.cs`, exercised in v05/v08 and Verification. Exercise 1 has its own solution directory.
- Design best practices: applied throughout the pack. Exercise 2 has its own solution directory.

## Relationship to the published full program

`Reference/GitBook.Program.cs.txt` is an exact extraction of the current C# code block. It is an audit/reference file, not another project to build. Source checksums are recorded in `Reference/source.json`.

The runnable v05 implementation factors that program into shared classes and a short section-specific entry point. It preserves the default synthetic caller, allowed services, catalog values, function descriptions, Azure CLI identity, function-loop configuration and five demonstration prompts. The shared host has a separate per-turn runtime/evidence object so Exercise 2 can share the same budget across tools.

Pack additions are automatic root `.env` loading, centralized dependency versions, a labelled offline model double, selectable deterministic error cases, an alternative injected catalog, JSON report files and independent checks. The registered base-agent instructions match the source. The contact-specific instruction is injected only by Exercise 2. The pack does not edit GitBook.

The original single-file program reports a flat per-turn list of tool results. The pack adds tool names/attempts to each evidence item so a multi-tool budget can be inspected. Reports omit generated display text; console output still presents the answer. Live transport-request counts remain unavailable, rather than being inferred from tool or iteration counts.
