# Validation

Prepared against the current GitBook Unit 5 page on 2026-11-03.

## Completed

- All thirteen solution projects compile with .NET SDK 10.0.100: nine examples, two possible exercise solutions, one common library and one verification application. The build returned zero warnings and zero errors.
- All eleven example and exercise executables ran in demo/local mode and returned the expected JSON structures. The v09 executable produced eighteen case reports with no candidate failures.
- Verification passed 135 offline assertions using actual MAF function invocation and generic typed-response parsing around a labelled deterministic client.
- All five distributed PowerShell scripts outside Reference parsed successfully with the PowerShell parser.

The assertions cover the nine acceptance cases under both prompts, tool selection, source attribution, instruction hashes, unset sampling parameters, multi-service clarification, input length, argument normalization, inaccessible and absent records, shared attempt counts, unsupported business fields, the free-text semantic-validation gap and process-variable precedence.

Exercise checks cover explicit UTC maintenance timestamps, window ordering, unavailable results without data, the three human-review cases, fabricated true flags and evidence concerning different services.

## Limits

No Azure CLI sign-in or live Azure inference was performed during preparation. The live client path is compiled and uses the source's AzureOpenAIClient, AzureCliCredential, resource-root endpoint and deployment variable names. The assigned deployment still needs to support tools and structured outputs together through Chat Completions.

The scripted demo intentionally follows a deterministic policy for both prompt versions. Its success is an integration check, not evidence that v1 and v2 have equal quality or that a real model will resist an injected instruction. Live outputs require the semantic review described by the topic.

PowerShell syntax was checked programmatically; the scripts were not run in a Windows PowerShell session during preparation.
