# MAF Unit 3 - agent fundamentals examples

Eight complete .NET 10 console projects implement every blue-example section of the current Unit 3. Shared modules and the only active `.env` live at the package root. All code comments, instructions and possible exercise solutions are in English.

## Contents

| Directory | GitBook section | Main behavior |
| --- | --- | --- |
| `v01-bounded-capability` | Formal definition of an agent | Read permitted status; refuse a restart capability that does not exist |
| `v02-tool-registration` | Tool registration | Inspect the function schema and invoke validation/authorization cases directly |
| `v03-conversational-context` | Conversational context management | Resolve a follow-up, then discard the old context after a reset |
| `v04-agent-lifecycle` | Agent lifecycle | Keep agent configuration while creating a new conversation |
| `v05-dependency-injection` | Dependency injection | Run the full five-message example; optionally inject an alternative catalog |
| `v06-result-management` | Result management | Distinguish a completed invocation from successful record retrieval |
| `v07-user-interaction` | User interaction | Clarify ambiguity and missing context; offer interactive input |
| `v08-error-handling` | Error handling | Distinguish business refusal, failure before/after tools, timeout and empty output |
| `Exercises/ex01-trust-boundaries` | Exercise 1 | Possible design and executable synthetic ticket/approval solution |
| `Exercises/ex02-support-contact` | Exercise 2 | Second injected read tool with the same per-turn budget |

`Shared/Unit03.Common` contains reusable configuration, catalog access, caller scope, tool execution, session lifecycle and model connection. `Scripts` contains PowerShell launchers. `Verification` runs independent behavior checks. Reports go to `output/` inside this package, created on first use. No model weights, credentials or SDK binaries are bundled.

`Execution control` has no standalone blue example in the current page: its controls are implemented in the shared host and tools and demonstrated by v05/v08 and Verification. `Design best practices` has no independent blue example: its principles apply across the pack, and Exercise 2 is solved separately. This preserves one example project per section with a blue example.

## 1. Configure the shared root file

Extract the complete ZIP and open PowerShell in `maf-unit03-examples`. Keep all project directories with `Shared`, `Directory.Build.props`, `Directory.Packages.props` and the solution file. Projects share source through project references; copying a single example directory is insufficient.

The included root `.env` is a nonsecret template. Edit it once. `.env.example` is a recovery template; no per-project `.env` files are needed. If obtaining the code from Git, where `.env` is ignored:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
```

The loader finds the package root from the working directory or compiled application location. It reads `NAME=value`, full-line `#` comments and matching single/double quotes. Inline comments, multiline values and variable expansion are unsupported. Nonempty process variables take precedence, matching the Unit 2 pack. Use a fresh terminal or remove a process override when changing an overridden value.

The default `.env` uses `MAF_EXECUTION_MODE=demo`. This requires no Azure identity and no deployment settings. `--demo` or `-Demo` explicitly selects it even when live mode is configured.

For live calls, fill `AZURE_OPENAI_ENDPOINT` with the HTTPS Azure OpenAI resource root and `AZURE_OPENAI_DEPLOYMENT_NAME` with the assigned deployment name. Fill `TENANT_ID` and `SUBSCRIPTION_ID` for the login helper. These are the same environment-variable names and Azure CLI identity pattern used in GitBook. The deployment must support Chat Completions and function calling; it consumes its Azure quota. `student-001` is a synthetic application caller, separate from the Azure CLI model-access identity.

## 2. Restore and build

Use the .NET **10 SDK**, GitBook's established package versions and PowerShell. Azure CLI is required only for live Azure authentication. The solution uses centrally pinned packages and includes a lock file per project. `Azure.AI.OpenAI` is a prerelease package.

```powershell
dotnet restore ./Maf.Unit03.slnx --locked-mode -m:1
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build ./Maf.Unit03.slnx --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
```

Restore needs NuGet access unless the required packages are already cached. The SDK `global.json` permits compatible .NET 10 feature bands. Serial build commands also limit build-worker resource usage.

## 3. Run examples in demo mode

```powershell
& ./Scripts/Run-Example.ps1 -Example v01 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v02 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v03 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v04 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v05 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v06 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v07 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example v08 -Demo -NoBuild
```

Demo mode uses a labelled deterministic **scripted model double** inside the real MAF `ChatClientAgent` and `FunctionInvokingChatClient` pipeline. MAF still invokes the registered functions and stores session history. Synthetic client-request counts describe that local simulation; they are not Azure usage or model-quality measurements. Timings measure local work only. No automatic fallback from a failed live request to the scripted model exists.

The v02 registration example always runs locally and makes no model requests, in either mode. Exercise 1 also always runs locally. Other projects call the assigned Azure model when `-Live` is selected.

## 4. Run against the assigned Azure deployment

```powershell
& ./Scripts/Login-Azure.ps1
& ./Scripts/Run-Example.ps1 -Example v01 -Live -NoBuild
& ./Scripts/Run-Example.ps1 -Example v05 -Live -NoBuild
& ./Scripts/Run-Example.ps1 -Example v06 -Live -NoBuild
& ./Scripts/Run-Example.ps1 -Example v08 -Live -Case business -NoBuild
```

The login helper loads the same root `.env`, runs tenant-specific `az login` and selects the subscription. Application authentication uses `AzureCliCredential`; no API key is stored. A live failure returns an error and is not silently replayed. Azure SDK retries may add HTTP attempts within the cancellation budget.

You can also run any project directly; it loads the root `.env` automatically:

```powershell
dotnet run --project ./v05-dependency-injection/DependencyInjection.csproj --no-build -- --live
```

## 5. Interactive use and dependency replacement

```powershell
& ./Scripts/Run-Example.ps1 -Example v04 -Demo -Interactive -NoBuild
& ./Scripts/Run-Example.ps1 -Example v07 -Live -Interactive -NoBuild
& ./Scripts/Run-Example.ps1 -Example v05 -Demo -Case alternate -NoBuild
& ./Scripts/Run-Example.ps1 -Example v01 -Demo -InputText 'What is the current status of VPN?' -NoBuild
```

Interactive commands are `/new` and `/exit`. Try payroll, its recovery time, CRM, billing and a restart request. The default catalog returns degraded payroll without a recovery time, healthy VPN, inaccessible CRM and absent billing. A new conversation does not retain the previous service reference. The alternative injected catalog returns healthy payroll with source `alternate-synthetic-catalog`.

The adapter serializes tool calls and the host rejects overlapping turns on one instance. A hosted application must use separate caller and conversation scopes. The included permission/identity data is deliberately synthetic.

## 6. Error cases and execution limits

```powershell
& ./Scripts/Run-Example.ps1 -Example v08 -Demo -Case before-tool -NoBuild
& ./Scripts/Run-Example.ps1 -Example v08 -Demo -Case after-tool -NoBuild
& ./Scripts/Run-Example.ps1 -Example v08 -Demo -Case timeout -NoBuild
& ./Scripts/Run-Example.ps1 -Example v08 -Demo -Case empty -NoBuild
& ./Scripts/Run-Example.ps1 -Example v08 -Demo -Case loop -NoBuild
```

The first case fails before any catalog access; the second retains evidence of an already executed lookup. Timeout and empty-response cases reset the affected conversation. These faults are deliberately synthetic and require demo mode; the timeout case uses a short local test deadline. The loop case demonstrates that a function-iteration bound and a tool-body access budget are separate controls.

Defaults match the GitBook application: 2,000 input characters, 45 seconds per turn, four configured function iterations, 800 output tokens, three tool-body attempts per turn and eight completed turns per conversation. Tunable values are in the shared `.env`. An iteration limit may still allow a final model request with tools disabled. A batch can contain several tool requests. Additional attempts do not read the catalog after the tool-body budget is exhausted. Cancellation is cooperative and does not roll back prior work.

## 7. Possible exercise solutions

```powershell
& ./Scripts/Run-Example.ps1 -Example ex01 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example ex02 -Demo -NoBuild
& ./Scripts/Run-Example.ps1 -Example ex02 -Live -NoBuild
```

See `Exercises/README.md` and the solution-specific READMEs. They are instructor reference implementations, with a design table for Exercise 1 and an executable contact tool for Exercise 2. Both remain separate from the eight example projects. This request does not change the student-facing GitBook exercise statements.

## 8. Read evidence and verify locally

```powershell
& ./Scripts/Test-Offline.ps1
```

The script performs locked restore/build, checks the agent/tool/session policies, runs all eight examples and both solutions in demo mode, and checks the alternative injected catalog. Restore still needs NuGet access if packages are missing.

Audit events contain run/conversation identifiers, status, elapsed time and structured tool evidence. `completed` describes a completed invocation, not successful fulfillment of every business request. Failed or absent records return no data. Run reports omit generated display text; the answer is printed in the console. Live model-request counts are `null` because the host does not instrument the provider transport. The application does not configure Application Insights or an OpenTelemetry exporter.

See `GITBOOK-MAPPING.md` for the exact coverage/refactoring notes and `VALIDATION.md` for the checks actually performed while building this ZIP.
