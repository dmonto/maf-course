# GitBook source alignment

Source: [Unit 8](https://app.gitbook.com/s/5NyZDQFujwe6GeJ3CBCA/session-4/unit-8), read on 2026-11-06. The read-only snapshot and its SHA-256 are in `Reference`.

| Current section | Project | Source relationship |
| --- | --- | --- |
| Coordinator-specialist architecture | v01 | Blue preparation example plus the coordinator/specialist responsibility table |
| Task delegation | v02 | Blue contracts example and bounded assignment behavior |
| Conflict resolution | v03 | Blue forced-conflict command; unsupported status rejected before synthesis |
| Inter-agent communication | v04 | Narrative task/report protocol made executable as an in-process JSON round trip |
| Role management | v05 | Narrative permission/recommendation table made executable with a disallowed-action check |
| Shared state control | v06 | Narrative case ownership and concurrent isolation made executable |
| Centralized supervision | v07 | All three blue examples (complete team, build/run and live Azure execution) in one project |
| Multi-agent scalability | v08 | Blue required-specialist failure example plus the narrative per-case versus host concurrency example |
| Collaborative patterns | v09 | Narrative sequential/concurrent comparison made executable through the same supervisor |
| Coordination best practices | v10 | Blue five-case control-flow verification matrix |
| Exercise 1 | Exercises/ex01-scoped-identity-delegation | Separate possible solution to the current identity-delegation statement |
| Exercise 2 | Exercises/ex02-idempotent-report-acceptance | Separate possible solution to the current idempotent-reducer statement |

## Preserved behavior

The shared library retains the published contracts, VPN/payroll observations, role instructions, recommendation allowlist, conflict injection, no-synthesis-on-required-failure rule and application-calculated business stage. MAF uses `RunAsync<Advice>` and `RunAsync<Brief>` with fresh sessions. Azure and MAF package versions match the current listings.

## Packaging changes

* The original single application is split into ten entry projects and a shared library. Model setup, policy and supervisor code are not duplicated between the projects.
* One literal `.env` replaces repeated terminal settings. The existing Azure CLI authentication flow is preserved; `Scripts/Login-Azure.ps1` imports this file before login.
* A uniform `Example`/`Mode`/`Stage`/`Data` JSON wrapper surrounds each demonstration. In v07, `Data` contains the published case outcome fields.
* Configurable demo delays, per-case concurrency of one or two and a serial comparison support observable timing. Defaults retain the same maximum of two specialists and 30/30/90-second deadlines.
* Binding checks for transported reports reject a changed run, task or assigned observation. These make the communication boundary testable.
* The instructor solutions add identity scoping and a replay reducer only inside `Exercises`. Baseline examples do not implement those solutions.

`AsAIFunction`, handoff, group chat and Magentic are discussed by the source as alternative mechanisms. v04 demonstrates the source's typed message protocol; v09 demonstrates application-owned sequential/concurrent scheduling. Neither project substitutes an unlabelled simulation of those SDK orchestrators.

The GitBook page has not been edited as part of preparing this code package.
