# MAF Unit 12: executable examples

Agent Evaluation and Testing: Metrics, Regressions and Microsoft.Extensions.AI.Evaluation.

Nine independent .NET 10 projects cover the nine GitBook sections containing an example, including the narrative examples without C# listings. Stable implementation lives in `Shared/Unit12.Common`. Every project reads the one unit-root `.env`. 

Source: [Unit 12 in GitBook](https://app.gitbook.com/s/5NyZDQFujwe6GeJ3CBCA/session-6/unit-12), read on 7 November 2026. `Reference/unit12-gitbook.md` preserves the source, and `Reference/OriginalListing` preserves its complete original listings. This pack reorganizes that application's implementation into reusable components and separate entry points. GitBook was read without modification.

## Install and build

Use the established .NET 10 SDK, Git and PowerShell setup. This directory belongs under your existing `maf-course` checkout. A ZIP delivery contains the single top-level directory `maf-unit12`; extract it into the checkout without removing other units.

From the repository root:

```powershell
Set-Location .\maf-unit12
.\Scripts\Initialize-Environment.ps1
dotnet restore .\Maf.Unit12.slnx --locked-mode -m:1
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build .\Maf.Unit12.slnx --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
.\Scripts\Run-Example.ps1 -Example v01 -Mode fixture -NoBuild
```

Keep the entire directory, including the solution, `Shared`, configuration files and lock files. A later .NET 10 SDK feature band is accepted. The first restore downloads the pinned NuGet dependencies; fixture execution subsequently needs no Azure connection or credentials.

The initializer preserves an existing `.env`. Shipped `.env` and `.env.example` contain matching empty assignments and local defaults. The loader interprets literal assignments, strips matching quotes, never executes expressions and preserves nonempty process environment variables. It locates the unit from the executable directory, including when launched from another working directory.

## Projects and expected observations

| ID | GitBook section | Directory | Default result / exit |
| --- | --- | --- | --- |
| v01 | Automated response evaluation | `v01-automated-response-evaluation` | Four isolated cases pass; JSON report; 0 |
| v02 | Quality metrics | `v02-quality-metrics` | Invalid metric examples rejected; exact checks' semantic limitation shown; four fixture cases pass; 0 |
| v03 | Regression testing | `v03-regression-testing` | Good run returns 0; bad run returns 1; regression demonstration returns 0 |
| v04 | Comparative model evaluation | `v04-comparative-model-evaluation` | Paired controlled fixture detects a bad candidate; 1 |
| v05 | Tool validation | `v05-tool-validation` | Actual function, argument, evidence and checks printed; 0 |
| v06 | Drift control | `v06-drift-control` | VPN failure with unchanged evidence yields a review signal; 1 |
| v07 | Testing automation | `v07-testing-automation` | Full runner; four fixture cases pass; 0 |
| v08 | Integration into CI/CD | `v08-ci-cd-integration` | Positive and negative gate expectations both satisfied; 0 |
| v09 | Result analysis | `v09-result-analysis` | Known failing fixture inspected; escaped HTML evidence report generated; analysis operation returns 0 |

The expected rejection in v04 and the diagnostic signal in v06 return 1 deliberately. v03 and v08 validate an expected rejection and return 0 when their demonstrations succeed. v09 returns the analysis operation's status; its HTML retains the candidate's original exit code. A script must account for these different operations.

```powershell
.\Scripts\Test-All.ps1 -NoBuild
```

## One shared environment

| Setting | Meaning |
| --- | --- |
| `EVAL_MODE` | Default for direct application execution: `fixture`, `fixture-bad` or `azure` |
| `EVAL_REPETITIONS` | 1–5 independent repetitions per case; default 1 |
| `TENANT_ID`, `SUBSCRIPTION_ID` | Assigned Azure CLI sign-in context |
| `AZURE_OPENAI_ENDPOINT` | Resource-root HTTPS endpoint, e.g. `https://RESOURCE.openai.azure.com/` |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | Assigned assistant deployment; baseline for v04 |
| `EVAL_CANDIDATE_DEPLOYMENT_NAME` | Optional assistant override; required second deployment for live v04 |
| `EVAL_JUDGE_DEPLOYMENT_NAME` | Assigned, compatible Chat Completions evaluation deployment |
| `EVAL_AGENT_MODEL_NAME`, `EVAL_AGENT_MODEL_VERSION` | Optional supplied model identity for individual runs |
| `EVAL_BASELINE_MODEL_*`, `EVAL_CANDIDATE_MODEL_*` | Separate optional identities for v04's A/B runs |
| `EVAL_JUDGE_MODEL_NAME`, `EVAL_JUDGE_MODEL_VERSION` | Optional supplied judge identity |
| `EVAL_COMMIT` | Source revision associated with a run |

Optional identities are recorded as null if unknown; the pack does not infer a model version from its deployment name. `Run-Example.ps1 -Mode` explicitly selects the mode and defaults to fixture. A direct `dotnet run` without a mode uses `EVAL_MODE`. An existing process variable takes precedence over the `.env`; clear it or open a new terminal when intentionally replacing it.

## Fixture and Azure evaluation

`fixture` calls the synthetic read tool directly and produces the supported answer. `fixture-bad` changes only the `vpn-status` answer to `healthy` while its tool still returns `degraded`. Both record `judgeStatus=not_run` and contain no model scores. They test the harness, not inference or model quality.

```powershell
.\Scripts\Run-Example.ps1 -Example v07 -Mode fixture -NoBuild
.\Scripts\Run-Example.ps1 -Example v07 -Mode fixture-bad -NoBuild
if ($LASTEXITCODE -ne 1) { throw 'The known bad fixture must return 1.' }
```

`azure` creates a fresh MAF agent and fresh recorded tools for each sample. The agent uses `AsAIAgent`, `AIFunctionFactory.Create`, `RunAsync`, JSON output and a bounded function-invocation client. It reads only synthetic service evidence; no repair or notification tool exists. Relevance and groundedness are evaluated separately through the reporting library, with actual tool evidence supplied as grounding context. Failed cloud runs remain failed.

The assigned assistant must support Chat Completions, JSON and function calling. The judge must support the generation settings required by the quality evaluators, including temperature and penalties. Confirm compatibility before assigning a reasoning deployment as judge. Azure uses `AzureCliCredential` and the user's existing assigned resource access.

Edit the shared `.env`, then:

```powershell
.\Scripts\Login-Azure.ps1
$env:EVAL_COMMIT = git rev-parse HEAD
.\Scripts\Run-Example.ps1 -Example v07 -Mode azure -NoBuild
.\Scripts\New-QualityReport.ps1 -Example v07
```

A single full run has four assistant scenarios and eight evaluator invocations, plus model turns needed for tool calls. Repetitions increase consumption. Agent latency stops before judge work; token usage and monetary cost are not measured. There is a two-minute total sample budget and a maximum of three function-invocation iterations. Judge-response caching is disabled.

## Paired comparison and drift diagnosis

```powershell
.\Scripts\Run-Example.ps1 -Example v04 -Mode azure -NoBuild
.\Scripts\New-QualityReport.ps1 -Example v04 -Label baseline
.\Scripts\New-QualityReport.ps1 -Example v04 -Label candidate
```

v04 first requires an accepted baseline, then runs the second assigned deployment with the same cases, evidence, instructions, judge and rubric. Its comparison pairs by case ID and iteration and rejects incompatible metadata or missing pairs. Per-case quality deltas and agent latency deltas are retained. A four-case smoke suite is not a statistically reliable model ranking.

You can also compare retained files without new inference:

```powershell
.\Scripts\Run-Example.ps1 -Example v04 -Baseline .\artifacts\v04\baseline\latest.json -Candidate .\artifacts\v04\candidate\latest.json -NoBuild
.\Scripts\Run-Example.ps1 -Example v06 -Baseline .\artifacts\v04\baseline\latest.json -Candidate .\artifacts\v04\candidate\latest.json -NoBuild
```

v06 identifies observable configuration/evidence changes and retained-case failures. It supports investigation; it implements no automatic statistical detector or causal attribution.

## Reports and exit codes

Reports live under `artifacts/<example>/`, with separate labels for paired runs. `run-<unique-id>.json` retains history; `latest.json` points to the latest result in that scope. Reports contain expected-dataset, prompt and fixture hashes, package versions, deployments, optional model identities, tool evidence, per-check decisions, valid quality scores when run, and agent-only latency.

| Runner exit | Meaning |
| --- | --- |
| 0 | Every check required by the selected mode passes |
| 1 | A business invariant or valid quality threshold fails |
| 2 | Configuration, execution or evaluation is inconclusive |

Missing, nonfinite, out-of-range or diagnostic-bearing quality metrics are inconclusive. Missing required JSON fields and duplicate properties are rejected. High quality scores cannot excuse a permission, status, action or tool-trace failure.

v09 generates an evidence HTML from the pack's run JSON:

```powershell
.\Scripts\Run-Example.ps1 -Example v09 -Report .\artifacts\v07\latest.json -NoBuild
```

The separate `aieval` HTML uses the reporting library's `judged` scenario store created during Azure runs. `New-QualityReport.ps1` refuses a missing store, restores the pinned local tool and invokes `aieval`. The pack's fixture JSON cannot substitute for that store.

The workflow template is `.github/workflows/unit12-evaluation.yml` **inside this unit**. To enable it, copy it to the repository's root `.github/workflows/` directory. It uses locked restore, Release build, a real child-process fixture gate, verification and always-retained artifacts. It does not authenticate to Azure. Run authenticated model evaluation separately before accepting a model release. The workflow has not been installed in the repository root or run on GitHub.

## Validation and references

See `VALIDATION.md` for checks completed on this pack and their limits, and `GITBOOK-MAPPING.md` for the complete topic mapping.

Primary API references: [evaluation libraries](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/libraries), [response evaluation](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/evaluate-ai-response), [reporting and caching](https://learn.microsoft.com/en-us/dotnet/ai/evaluation/evaluate-with-reporting).
