# MAF Unit 2 - model ecosystem examples

Eight complete .NET 10 console projects implement every blue-example section of Unit 2, including conceptual examples that contained no code. The shared `IChatClient` layer is the model connection used underneath chat-client-based MAF agents; this unit does not add tools, persistence or orchestration. An optional Python MAF agent reference is included with the endpoint probe.

The runtime choices are **Azure OpenAI**, **Microsoft Foundry Models** and **Foundry Local**. The Foundry catalog comparison uses compatible, assigned deployments; it does not depend on the retired GitHub Models inference API.

## Contents

| Project directory | GitBook topic | Calls in a normal live run |
| --- | --- | ---: |
| v01-foundry-architecture | Execution boundaries | 1 |
| v02-azure-openai | Fast vs quality deployment | 2 |
| v03-foundry-models | Foundry ticket classifiers | 6 |
| v04-foundry-local | Local preparation and inference | 1 |
| v05-endpoint-management | Complete configurable connection probe | 1 |
| v06-model-versioning | Baseline/candidate regression comparison | 6 |
| v07-consumption-cost | Call and case cost | 0 |
| v08-model-observability | Repeated requests and operational evidence | 3 by default |
| Exercises/ex01-provider-selection | Possible solution to Exercise 1 | 0 |
| Exercises/ex02-provider-comparison | Possible solution to Exercise 2 | 2 |

`Shared/Unit02.Common` contains reusable configuration, clients, synthetic task data, quality checks, metrics, cost calculations and selection helpers. `Verification` provides independent checks. `Scripts` contains preparation and run commands. Reports are written under `output`, created on first run. No model weights or SDK binaries are bundled.

## 1. Configure once

Extract the complete ZIP and open PowerShell in its root. Install the .NET 10 SDK. The included `.env` is a blank, nonsecret template shared by all projects; edit it once. `.env.example` is a recovery copy of that template. Never create per-project copies.

The parser supports `NAME=value`, full-line comments and matching single or double quotes. It does not expand variables or support inline comments. Existing process variables take precedence, which allows a PowerShell override. Start a fresh terminal or remove an old process override when changing a value that is already set.

The default provider is `foundry-local`. Set its full model ID in `.env`; do not use an Azure deployment name in that setting. For Azure examples, set the assigned endpoint and deployment names, including fast/quality targets where required. Provider-specific values may stay blank until that provider is used.

If obtaining these files from a Git repository where `.env` is ignored, create the shared file once:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
```

## 2. Restore and build

```powershell
dotnet restore ./Maf.Unit02.slnx --locked-mode -m:1
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build ./Maf.Unit02.slnx --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
```

Package versions are centralized and locked. Azure.AI.OpenAI is a prerelease. The Entra-authenticated Foundry path uses an OpenAI SDK constructor marked experimental; its diagnostic is suppressed only around that constructor. The provided clients use Chat Completions for consistency across the selected providers.

## 3. Try the examples without inference

```powershell
& ./Scripts/Run-Example.ps1 -Example v01 -Demo
& ./Scripts/Run-Example.ps1 -Example v03 -Demo
& ./Scripts/Test-Offline.ps1
```

Demo mode is explicitly synthetic: no model requests, credentials or real consumption. Its timings and token counts are not measurements. Package restore still needs access to NuGet unless the packages are already cached. The offline test builds and runs the checks, all eight demonstrations and both exercise solutions.

## 4. Prepare and use Foundry Local

Install/update Foundry Local before class:

```powershell
winget install --id Microsoft.FoundryLocal --exact
# For an existing installation:
# winget upgrade --id Microsoft.FoundryLocal
```

Reopen PowerShell. Run `foundry model list`, then copy the assigned full hardware-specific model ID into the root `.env`. The scripts use the current `foundry server` CLI commands.

```powershell
& ./Scripts/Prepare-FoundryLocal.ps1
& ./Scripts/Run-Example.ps1 -Example v04
& ./Scripts/Run-Example.ps1 -Example v05 -Provider foundry-local
```

The preparation script restarts the local lesson server on the port in `FOUNDRY_LOCAL_BASE_URL`, downloads and loads the chosen model, and lists loaded model IDs. The template explicitly selects loopback port 39839. If it is occupied, change the root base URL to another port before preparing. Confirm the active address with `foundry server status`.

Prepare packages, model weights and execution providers before testing offline. Record the selected execution provider and hardware. After finishing the local comparisons, release memory with `foundry model unload` followed by the assigned model ID, and stop the lesson server with `foundry server stop` if it is no longer needed.

## 5. Use Azure and Foundry Models

```powershell
& ./Scripts/Login-Azure.ps1
& ./Scripts/Run-Example.ps1 -Example v02
& ./Scripts/Run-Example.ps1 -Example v03
& ./Scripts/Run-Example.ps1 -Example v05 -Provider azure
```

Azure OpenAI uses `AZURE_OPENAI_ENDPOINT` as an HTTPS resource root and Azure CLI identity. Foundry Models uses `FOUNDRY_MODELS_BASE_URL` as a supported HTTPS `/openai/v1/` endpoint. `FOUNDRY_AUTH_MODE=entra` uses Azure CLI with the Foundry scope; `key` mode reads the root `FOUNDRY_MODELS_API_KEY`. Use the actual permissions and endpoint supplied for the course. These programs do not create cloud resources or deployments.

Choose models supporting the API and requested task. A Foundry project endpoint ending in `/api/projects/...` is a different SDK boundary and is rejected by these inference examples. The local path requires a loopback `/v1/` address and uses an ignored SDK key placeholder.

## Read the results

Each model call records provider, requested and returned model identifiers, response ID, status, elapsed milliseconds, reported tokens, phase and fact-check outcome. Missing usage remains `null`. SDK retries are disabled so recorded requests correspond to one attempt. Clients are reused within each process.

The summary checker is a deliberately small lexical check for the supplied service, impact and unknown recovery time; manually review the answer as well. It is not a general semantic evaluator. Connection success does not imply task success. Outputs save metadata and check results without raw prompts or answers; model text appears only in the console by default.

`elapsedMs` measures the SDK request, including authentication/model loading when that happens during the request. It excludes process startup and client construction. The Python reference measures the agent run. Keep timing boundaries aligned when making comparisons. A few samples or demo outputs do not establish p50/p95 performance for a real workload. The observability example does not install an OpenTelemetry exporter or submit logs to Application Insights.

## Exercise solutions and mapping

See [Exercises/README.md](Exercises/README.md) for the instructor-only possible solutions and [GITBOOK-MAPPING.md](GITBOOK-MAPPING.md) for exact section mapping. The projects are separate applications, not incremental patches: keep the shared root alongside each project.

## Troubleshooting

| Symptom | First check |
| --- | --- |
| Missing setting | Edit the one root .env; inspect process overrides |
| Azure 401/403 | Azure CLI identity, inference role, scope, propagation and network access |
| 404 or unsupported API | Resource root vs v1 URL, deployment name and model/API compatibility |
| 429 | Assigned capacity and rate limits; this pack does not add automatic retries |
| Local connection refused | Foundry Local active host/port matches the root base URL |
| Local model not loaded | Full model ID and foundry model load output; run preparation again |
| Quality check fails | Inspect task facts; do not silently count it as success |
| Lock restore fails after intentional package edits | Revalidate dependency changes and regenerate the affected locks with unlocked restore |

## Sources

* [Unit 2](https://diego-montoliu.gitbook.io/maf-course/session-1/unit-2)
* [IChatClient](https://learn.microsoft.com/en-us/dotnet/ai/ichatclient)
* [Foundry inference endpoints](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/endpoints)
* [Foundry Local CLI](https://learn.microsoft.com/en-us/azure/foundry-local/reference/reference-cli)
* [Foundry Local SDK integration](https://learn.microsoft.com/en-us/azure/foundry-local/how-to/how-to-integrate-with-inference-sdks)
