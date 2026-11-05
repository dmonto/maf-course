# Source mapping and packaging changes

Source: [Unit 7](https://diego-montoliu.gitbook.io/maf-course/session-3/unit-7), read on 2026-11-05 from its current main content. The title is “Workflows in MAF: Task Graphs, Decisions and Humans in the Loop”. No GitBook content was modified while creating this package.

| GitBook heading | Example/project | Relationship to the source |
| --- | --- | --- |
| Definition of task graphs | v01 | Preparation and graph execution; complete runnable project |
| Conditional transitions | v02 | Shared original records and exclusive routing; both paths runnable |
| Error handling in workflows | v03 | Narrative `--fail` timeout scenario made into its own project |
| Human-in-the-loop integration | v04 | Original complete executors, typed request port and review host |
| Intermediate validations | v05 | Build and three-outcome example, plus invalid-input check |
| Sequential orchestration | v06 | Narrative order-inspection example with a machine-checked completion order |
| Task parallelization | v07 | Narrative `Task.WhenAll` inspection example with measurable delays and `--serial` comparison |
| Workflow persistence | v08 | Original file checkpoint pause/resume flow, run in separate processes |
| Workflow observability | v09 | Original console event/live-drafting example with elapsed metadata |
| Real-world enterprise use cases | Exercise 2 | Design table and exercise; no separate blue runnable example |
| Exercise 1 | Exercises/ex01-evidence-quality-gate | Possible solution, isolated from examples |
| Exercise 2 | Exercises/ex02-idempotent-escalation | Possible solution, isolated from examples |

## Preserved behavior

- .NET 10 and the source's pinned Azure/MAF package versions.
- The same synthetic case, statuses, guide text, policy and drafting instructions.
- Drafting happens after evidence retrieval; a degraded service requires review.
- Typed messages and scoped shared proposal state.
- A maximum of three status-read attempts and a bounded live draft call.
- File-backed JSON checkpoints and stable executor/port IDs.
- `ConfigureProtocol(...).YieldsOutput<Outcome>()` on the baseline output executors.
- Baseline approval only changes the proposal outcome; it performs no external write.

## Packaging additions

The source has four listings that together form one application reused by several sections. This package compiles that stable implementation once as `Shared/Unit07.Common` and gives every example section its own executable project. The explicit narrative timeout example also gets a project.

The original `student.env.ps1` dependency becomes one package-root `.env`, with literal parsing, an initializer and an Azure-login helper. Process variables still work. No secrets are committed.

Project-relative data folders and `--state` isolate runs. Provider/configuration checks replace the original minimal bookmark check. New `--decision yes|no` values are labelled synthetic CLI test decisions. Retry-attempt and evidence-duration metadata make the narrative examples inspectable. `--serial` is a comparison option; the default retains the source's local parallelism.

The host buffers business outputs until the event stream and optional order check succeed. It still reports workflow events as they occur. A review port may complete twice, once for the request and once for the response; v06 checks both phases.

The baseline has no timestamp gate, escalation repository, write executor or idempotency mechanism. Those additions are confined to the exercise projects. The exercise-2 recovery host opts into a failed-run bookmark so the post-write failure scenario can resume.

All source-code comments and instructional text in the package are in English. This is a versioned teaching package, not a reimplementation using whichever newer MAF APIs happen to be available.
