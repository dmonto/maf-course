# Unit 5 GitBook mapping

Source: https://diego-montoliu.gitbook.io/maf-course/session-2/unit-5

| Project | GitBook H2 | Runnable example |
| --- | --- | --- |
| `v01` | Prompt design principles | Compares the minimal v1 and explicit v2 instructions using the same unsupported recovery-time claim. |
| `v02` | Separation between system and user instructions | Keeps application instructions in ChatOptions.Instructions and user text in RunAsync, then compares v1/v2 on an instruction-conflict request. |
| `v03` | Controlled tool calling | Runs the four narrative tool-selection examples: status, guidance, both, and missing subject. |
| `v04` | Input validation | Executes the tool adapter directly against normalized input, invalid syntax, inaccessible CRM, absent billing, and the shared attempt budget. |
| `v05` | Prevention of unwanted behaviors | Runs the payroll probe with injection enabled, matching the topic's synthetic attack in the tool Detail field. |
| `v06` | Encapsulation of logic in tools | Runs the complete two-tool application through shared configuration, registration, bounded invocation and output validation. |
| `v07` | Structured responses | Requests SupportAnswer through RunAsync<SupportAnswer> and validates the generated business fields against the actual tool results. |
| `v08` | Temperature and parameter control | Repeats the exact controlled comparison from Temperature and parameter control: one request, v1 then v2, fixed deployment and generation settings. |
| `v09` | Prompt testing | Runs the nine-case acceptance matrix for both instruction versions, using fresh tools and evidence for every case. |


The tenth H2, Enterprise best practices, has a review table and Exercise 2 but no separate example. Its possible solution is under Exercises/ex02-human-review. The maintenance contract exercise from Prevention of unwanted behaviors is under Exercises/ex01-maintenance-contract.

## Shared-code extraction

The source presents one console application across several sections. Each ZIP example is an independent executable project referencing stable code at the root. Both original prompt files are preserved. ServiceTools retains the catalog, caller scope, result shape, injection fixture and shared three-attempt budget. AnswerValidator retains its original business-field checks, including the documented free-text semantic-validation gap.

Contracts are public for cross-project references. SupportAnswer is inheritable so the separate second exercise can extend it without copying baseline fields. Ordinary examples still request precisely the baseline output fields.

The root .env replaces manually sourcing student.env.ps1. Azure endpoint, deployment and PROMPT_VERSION variable names match the source. The app resolves its prompt files from the package root rather than copying them into each project's output. Run metadata adds explicit execution mode, prompt hash and parameter values; this does not change the business contract.

The source's Test-Prompts.ps1 is adapted only to target v06, write output beneath the package root, and accept explicit demo/live switches. It retains the nine inputs, business expectations and candidate-versus-baseline exit policy. The original extracted script is in Reference/GitBook-original for comparison.

No GitBook content is changed by creating this package. The Reference directory is a source snapshot, not an additional project to build.
