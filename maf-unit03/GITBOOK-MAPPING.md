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

## Relationship to the published full program

`Reference/GitBook.Program.cs.txt` is an exact extraction of the current C# code block. It is an audit/reference file, not another project to build. Source checksums are recorded in `Reference/source.json`.

The original single-file program reports a flat per-turn list of tool results. The pack adds tool names/attempts to each evidence item so a multi-tool budget can be inspected. Reports omit generated display text; console output still presents the answer. Live transport-request counts remain unavailable, rather than being inferred from tool or iteration counts.
