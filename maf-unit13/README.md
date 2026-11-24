# MAF Unit 13: executable examples

Security, Compliance and Data Governance for Agentic Solutions on Azure.

Ten independent .NET 10 projects cover the ten GitBook sections, including narrative examples without a C# listing. Stable implementation lives in `Shared/Unit13.Common`. All projects use one unit-root `.env`.

Source: [Unit 13 in GitBook](https://app.gitbook.com/s/5NyZDQFujwe6GeJ3CBCA/session-6/unit-13), read on 8 November 2026. `Reference/unit13-gitbook.md` preserves the source; `Reference/OriginalListing` preserves its complete original code. GitBook was read without modification.

## Install and build

Keep the complete `maf-unit13` folder under your existing `maf-course` checkout. A ZIP delivery contains exactly this top-level folder; extract it into the checkout. Existing units are independent of this package.

Requirements: .NET 10 SDK, PowerShell and Python 3.10 or later. Azure CLI is needed only for live model calls. The package uses the same pinned Azure OpenAI, Azure Identity and MAF versions as the GitBook listing. Package lock files are included.

From the repository root:

```powershell
Set-Location .\maf-unit13
.\Scripts\Initialize-Environment.ps1
dotnet restore .\Maf.Unit13.slnx --locked-mode -m:1
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet build .\Maf.Unit13.slnx --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
.\Scripts\Run-Example.ps1 -Example v01 -Mode fixture -NoBuild
```

The first restore downloads the declared packages. Fixture execution requires no Azure account or model connection. The initializer creates `.env` only when missing, preserving an existing file.

## Projects and expected observations

| ID | GitBook section | Directory | Expected observation |
| --- | --- | --- | --- |
| v01 | Granular access control | `v01-granular-access-control` | Seven policy decisions; granted and denied reads match the contract |
| v02 | Identity management | `v02-identity-management` | Trusted fixture claims mapped; unauthenticated caller and role spoof denied |
| v03 | Protection of sensitive data | `v03-sensitive-data-protection` | Three approved evidence fields; synthetic owner contact excluded |
| v04 | Interaction auditing | `v04-interaction-auditing` | Seven cases; correlated `audit.jsonl` and `results.json` |
| v05 | Retention policies | `v05-retention-policies` | Expired owned file deleted; held and current files preserved |
| v06 | Model exposure control | `v06-model-exposure-control` | One approved endpoint accepted; eight invalid destinations rejected |
| v07 | Risk assessment | `v07-risk-assessment` | Request-scope and tenant violations denied; audit outage withholds evidence |
| v08 | Regulatory compliance | `v08-regulatory-compliance` | Six controls mapped to evidence or organizational review; no compliance verdict |
| v09 | Pipeline governance | `v09-pipeline-governance` | Original Python gate accepts valid evidence and rejects changed policy |
| v10 | Enterprise best practices | `v10-enterprise-best-practices` | Combined suite and gate produce release-evidence metadata |

Every fixture demonstration returns `0` when its assertions pass. Failed suites or gates return `1`; configuration/setup failures return `2`. The expected negative child gate in v09 prints `SECURITY GATE FAIL` and returns `1`; v09 returns `0` only after confirming that this rejection occurred.

```powershell
.\Scripts\Run-Example.ps1 -Example v04 -Mode fixture -NoBuild
.\Scripts\Run-Example.ps1 -Example v09 -Mode fixture -NoBuild
.\Scripts\Run-Example.ps1 -Example v10 -Mode fixture -NoBuild
.\Scripts\Test-All.ps1 -NoBuild
```

## Shared configuration

The shipped `.env` and `.env.example` contain matching empty Azure assignments and safe local defaults. There is no per-project `.env`.

| Variable | Meaning |
| --- | --- |
| `SECURITY_MODE` | Default mode for direct `dotnet run`: `fixture` or `azure` |
| `SECURITY_RETENTION_DAYS` | Illustrative duration used by v05; 1–3650, default 30 |
| `PYTHON_COMMAND` | Python executable name or absolute executable path; default `python` |
| `TENANT_ID` | Assigned Azure CLI tenant, also bound to `AzureCliCredential` |
| `SUBSCRIPTION_ID` | Assigned subscription selected by the sign-in helper |
| `AZURE_OPENAI_ENDPOINT` | Assigned resource-root HTTPS URL, e.g. `https://RESOURCE.openai.azure.com/` |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | Assigned deployment supporting Chat Completions, JSON and function calling |
| `AZURE_OPENAI_APPROVED_HOSTS` | Exact approved host name(s), comma-separated, e.g. `RESOURCE.openai.azure.com` |

Values are literal assignments with optional matching single/double quotes. The loader does not evaluate expressions or interpolate variables. Existing nonempty process variables take precedence; open a new terminal or clear a variable when intentionally replacing it. `PYTHON_COMMAND` accepts an executable only, not a command with arguments such as `py -3`.

The unit root is found by walking upward from the executable directory. Examples can be launched from another working directory as long as the complete package remains present. Local outputs are written beneath this unit's `artifacts` directory.

## Real Azure-backed MAF runs

Only v04 and v10 support `-Mode azure`. Complete the assigned values in the shared `.env`, including the exact approved resource host, then:

```powershell
.\Scripts\Login-Azure.ps1
.\Scripts\Run-Example.ps1 -Example v04 -Mode azure -NoBuild
.\Scripts\Run-Example.ps1 -Example v10 -Mode azure -NoBuild
```

These paths use `AzureOpenAIClient`, `AzureCliCredential`, `AsAIAgent`, `AIFunctionFactory.Create`, bounded `FunctionInvokingChatClient` and `RunAsync`. Each scenario creates an independent agent and fresh request-bound tools. The tool loop is bounded at three invocation iterations and each scenario has a two-minute cancellation budget. A full run contains seven assistant scenarios plus their tool turns.

The Azure CLI identity authenticates the application to Azure OpenAI. Caller identities remain synthetic fixture principals. No HTTP user-authentication layer, JWT validation service, deployed private endpoint or Microsoft Purview policy is provisioned by this package. v02 accepts already-validated test claims and does not establish a hosted authentication boundary.

Fixture mode invokes the tool directly and generates its permitted evidence; it does not measure model behavior or prove prompt-injection resistance. Live mode requires actual tool use and a matching status contract. Provider-filter blocking, tool omission and invalid JSON remain failed cases until their intended contracts are explicitly reviewed. Model answer grounding and unsupported claims need the broader Unit 12 evaluation suite.

## Evidence and Python gate

Every run writes a unique directory under `artifacts/<example>/run-<timestamp>-<id>/`. The final `RESULT` line contains its exact path. No command selects an earlier successful run to replace failed evidence.

The v04 and v10 case IDs, policy version and audit/results schemas match the GitBook listing. `Python/security-gate.py` is copied verbatim from GitBook. It requires all seven baseline cases and correlates each outcome to an appropriate decision; it also rejects unexpected audit fields, email-shaped data and policy-version changes.

```powershell
python .\Python\security-gate.py 'C:\path with spaces\run-directory'
python .\Python\test-security-gate.py 'C:\path with spaces\valid-run-directory'
.\Scripts\Run-Example.ps1 -Example v09 -Report 'C:\path with spaces\run-directory' -NoBuild
```

The mutation checks use real generated evidence and confirm rejection of a missing case, unauthorized allow, sensitive audit field, changed policy and missing audit trail. The original valid evidence is preserved.

`FileAuditSink` flushes metadata before returning tool results. A local JSONL file is editable and is not a production tamper-resistant audit destination. Email detection is deliberately narrow. Classification strings are application fixture values; they are not Purview sensitivity-label integration. v05 manages only its own generated files and does not implement an organization-wide deletion or backup lifecycle.

## Pipeline command

From the unit root:

```powershell
.\Scripts\Run-SecurityPipeline.ps1
```

This performs locked restore, builds the solution and runs v10 in fixture mode. The actual suite/gate result controls the stage's success. Publish the exact reported directory as a restricted pipeline artifact with an approved retention rule. An authorized separate stage is needed for live model and hosted integration tests.

## Troubleshooting

| Symptom | Action |
| --- | --- |
| Python cannot start | Install Python and set `PYTHON_COMMAND` to its executable path |
| .NET SDK unavailable | Install a stable .NET 10 SDK; the local `global.json` accepts later feature bands |
| Locked restore fails | Retain the complete reviewed package and lock files; restore from the unit root |
| Live call fails before inference | Check tenant, assigned endpoint, deployment and the exact approved-host list |
| Azure 401/403/404/429 | Check sign-in, data-plane role, deployment name, quota and network access |
| Gate fails in Azure mode | Inspect case metadata and tool decisions; distinguish provider filtering from an authorization failure |
| v09 prints an expected rejection | Confirm its final exit is 0 and `gate-demonstration.json` records negative exit 1 |

Use the organization's approved PowerShell execution policy. Credentials and local `.env` values are excluded from Git; the distributed `.env` contains no secrets.
