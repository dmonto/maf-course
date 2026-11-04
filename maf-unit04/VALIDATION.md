# Validation

Prepared against the current Unit 4 GitBook page on 2026-11-03.

## Completed checks

- The exact extracted six-file GitBook application builds with .NET SDK 10.0.100.
- All fourteen projects in Maf.Unit04.slnx build: ten examples, two exercise solutions, one common library and one verification application. The build completed with zero warnings and zero errors.
- The verification application passed 145 assertions using the real MAF function/session pipeline, a labelled scripted model and actual loopback HTTP hosts.
- All seven distributed PowerShell scripts outside Reference parsed successfully with the PowerShell parser.

The HTTP checks cover the ten section profiles; health; invalid and unknown conversation inputs; payroll and VPN evidence; inaccessible CRM and absent billing; fresh versus reused sessions; unsupported writes; six-turn and twenty-conversation limits; concurrent requests; unrelated sessions; session invalidation after an execution failure; the dynamic /v1/responses endpoint; streamed completion; and DevUI route availability.

The Exercise 2 checks advance a controllable clock. They verify HTTP 410 for a recently expired session, successful immediate follow-up, capacity reclamation, startup configuration validation, and cleanup that does not expire an active turn.

The pinned Hosting.OpenAI version routes protocol requests using agent.name (or metadata.entity_id). Scripts use agent.name; they do not assume that a newer release's model-based routing is available.

## Scope of these results

No live Azure inference or Azure CLI sign-in was performed while preparing this pack. The live Azure client path is compiled and retains the topic's endpoint, deployment and credential configuration. Demo results do not establish live model behavior, answer quality, latency or token cost. The v10 matrix requires a semantic review of each live answer.

DevUI availability and its underlying streamed protocol were checked programmatically; a visual browser review was not performed. The instruction comparison is an experiment to run with the assigned deployment, rather than a claim that v2 always produces better answers.
