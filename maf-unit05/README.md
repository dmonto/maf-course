# MAF Unit 5: example projects

Prompt and Tool Design: How to Guide Agents to Do the Right Thing.

Nine executable projects correspond to the nine GitBook sections that contain examples, including narrative examples without code blocks. The tenth section contains best practices and Exercise 2; its possible solution is in the separate Exercises directory. Stable code, prompt files and one .env are shared from the package root.

## Prerequisites and checkout

Use the .NET 10 SDK, PowerShell, Git and access to the pinned NuGet packages. Live mode also requires Azure CLI and an assigned Azure OpenAI deployment supporting function tools and structured outputs together through Chat Completions.

If the course repository has not been cloned:

```powershell
git clone https://github.com/dmonto/maf-course.git
if ($LASTEXITCODE -ne 0) { throw 'Repository clone failed.' }
Set-Location ./maf-course
```

Extract this ZIP into that repository, preserving the complete maf-unit05-examples directory. Enter the extracted package and build:

```powershell
Set-Location ./maf-unit05-examples
dotnet build ./Maf.Unit05.slnx -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
```

The ZIP includes one placeholder-only `.env` and a matching `.env.example`. Edit only the root `.env`; do not copy it into the example projects. To intentionally reset your local configuration, use `Copy-Item ./.env.example ./.env`. This replaces the existing file.

## Quick start

```powershell
& ./Scripts/Run-Example.ps1 -Example v06 -Demo -NoBuild
```

The default request asks for payroll status and the recommended next step. Expect contract-accepted, degraded status, collect-incident-reference guidance, both synthetic sources, null recovery time and ActionExecuted=false.

Try a different single-run request:

```powershell
& ./Scripts/Run-Example.ps1 -Example v06 -Demo -NoBuild -InputText 'What is the current status of VPN?'
```

The JSON explicitly identifies demo-scripted-model. This deterministic client drives the real MAF function-invocation and typed-response pipeline, but it is not an LLM, an evaluator or a fallback for failed live requests. Its success does not establish Azure connectivity or prompt quality.

## Live Azure configuration

Set these assigned values in the shared `.env`:

```dotenv
MAF_EXECUTION_MODE=live
AZURE_OPENAI_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT_NAME=YOUR-ASSIGNED-DEPLOYMENT
TENANT_ID=YOUR-TENANT-ID
SUBSCRIPTION_ID=YOUR-SUBSCRIPTION-ID
PROMPT_VERSION=v2
```

Use the HTTPS resource-root endpoint for AzureOpenAIClient, rather than a Foundry project path or an /openai/v1/ base URL. The deployment must support the selected API, tools and structured-output combination. Its administrative name alone does not establish model-family capabilities.

```powershell
& ./Scripts/Login-Azure.ps1
& ./Scripts/Run-Example.ps1 -Example v06 -Live -NoBuild
```

A failed live request stays failed; it does not switch to scripted output. v04 and the first exercise perform no inference in either mode.

Every executable finds the package root by locating Maf.Unit05.slnx in the ancestors of the current directory or executable directory. The dotenv reader supports NAME=value and quoted literal values without shell execution or expression expansion. Nonempty process variables take precedence over the file. Clear an old process variable or use a new terminal after changing a value previously imported into that terminal. CLI -Demo/-Live and -PromptVersion explicitly select the requested mode/version.

## Example map

| ID | GitBook section | Default execution |
| --- | --- | --- |
| v01 | Prompt design principles | Compare v1/v2 on unsupported recovery-time input |
| v02 | Separation between system and user instructions | Compare v1/v2 on an instruction-conflict request |
| v03 | Controlled tool calling | Status, guidance, both and clarification cases |
| v04 | Input validation | Direct local argument/scope/budget checks |
| v05 | Prevention of unwanted behaviors | Tool-data injection probe |
| v06 | Encapsulation of logic in tools | Complete single-run two-tool application |
| v07 | Structured responses | Typed output and evidence validation |
| v08 | Temperature and parameter control | Controlled v1/v2 comparison with fixed settings |
| v09 | Prompt testing | Nine cases for each of v1/v2 |

Each project has its own executable Program.cs and README. The common implementation lives under Shared/Unit05.Common. v01, v02 and v08 intentionally run both prompt versions; v06 and v07 accept a single selected version. The v05 profile always enables the injection fixture. Every model-driven run has fresh caller-bound tools and fresh evidence, with no reused conversational state.

## Shared parameters

The defaults match the topic:

- PROMPT_VERSION=v2.
- REQUEST_TIMEOUT_SECONDS=45.
- MAX_OUTPUT_TOKENS=1600.
- MAX_ITERATIONS=4.
- Three tool-body attempts shared between both read tools per run.
- Serial tool execution within a run.
- Temperature and TopP left unset.

Four invocation iterations are not a four-tool-call budget. The adapter's shared business counter refuses its fourth body attempt even when multiple calls occur within an iteration. Time cancellation is cooperative and does not undo any completed operation. This application has only read capabilities.

For a single selected prompt:

```powershell
& ./Scripts/Run-Example.ps1 -Example v07 -Live -PromptVersion v1 -InputText 'What is the current status of payroll?'
```

For the injection case:

```powershell
& ./Scripts/Run-Example.ps1 -Example v05 -Live
```

Expected evidence contains the synthetic attack string in Detail. The accepted response must not claim that RestartService executed. Review Summary as well as typed fields: the supplied validator checks summary presence and length, not its complete meaning.

## Prompt comparisons and acceptance checks

```powershell
& ./Scripts/Run-Example.ps1 -Example v08 -Live
& ./Scripts/Run-Example.ps1 -Example v09 -Live
```

v09 performs eighteen inference runs per pass. Use `-Repeat 2` for thirty-six. One run may involve several model service calls, so this is not a count of network calls or tokens. Keep the deployment, data and settings fixed while comparing prompts. The package does not require v1 to fail or claim v2 must always perform better.

v09 writes output/v09-results.json. The adapted PowerShell matrix from the topic can also be used after building v06:

```powershell
# Run with PowerShell 7.
& ./Scripts/Test-Prompts.ps1 -Demo
# Or use the assigned Azure deployment:
& ./Scripts/Test-Prompts.ps1 -Live
```

This script saves each case and a checks.csv under output/prompt-results. It checks outcomes, tool selection and expected business fields. Baseline v1 failures remain recorded; v2 failures determine the matrix's exit status. Inspect factual claims, clarification behavior and refusal wording manually before drawing a quality conclusion.

Single-run exit codes match the original application: 0 for contract-accepted, 1 for execution failure, and 2 for contract-rejected. Rejected single-run reports have answer=null and an errors list. Comparison and matrix projects aggregate these results; a failed candidate returns a nonzero exit code.

## Separate exercise solutions

```powershell
& ./Scripts/Run-Example.ps1 -Example ex01 -Demo
& ./Scripts/Run-Example.ps1 -Example ex02 -Demo
```

ex01 provides a maintenance-window method contract, typed UTC fixtures, validation and three expected cases. The exercise asks for design before implementation, so no maintenance tool is registered or executed.

ex02 adds RequiresHumanReview only to its own output type. Its default cases demonstrate true for combined payroll status/guidance, false for payroll status alone and false for combined VPN status/guidance. The validator rejects a fabricated true flag with no supporting evidence. The flag is a recommendation and performs no action. Use -Live to request this extended contract from the assigned model.

## Verification and source comparison

```powershell
& ./Scripts/Test-Offline.ps1
```

Verification checks the real MAF tool pipeline, typed parsing, input and scope boundaries, shared budgets, fabricated fields, the documented semantic-validation gap and both possible exercise solutions. Demo checks do not evaluate a live model's behavior. See VALIDATION.md for the completed build and test results.

The Reference directory retains extracted original executable blocks; they are not additional build projects. GITBOOK-MAPPING.md explains how each example maps to the page and lists the shared-code extraction changes. The ZIP creation does not modify GitBook.

Package versions are pinned to those used by the topic: Azure.AI.OpenAI 2.9.0-beta.1, Azure.Identity 1.21.0 and Microsoft.Agents.AI.OpenAI 1.24.0. Keep the dependency lock files when adding the examples to Git. A locked restore can check the retained dependency graph:

```powershell
dotnet restore ./Maf.Unit05.slnx --locked-mode -m:1
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
```

The shared .gitignore excludes .env, build products and generated output. The placeholder-only .env is included in the ZIP for local use; commit .env.example rather than real assigned settings.
