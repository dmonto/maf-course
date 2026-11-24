# MAF Course - Unit 1 example code

Introduction to Microsoft Agent Framework: Successor to Semantic Kernel and AutoGen.

## Contents

| Path | Purpose |
| --- | --- |
| `v01-native-integration/` | The read-only service-status agent from section 7 |
| `shared/` | Shared .env loading, Azure connection, console execution and synthetic catalog |
| `scripts/` | Shared configuration and evidence-recording helpers |
| `Directory.Packages.props` | The package versions  |
| `docs/source-map.md` | Mapping between GitBook sections and this package |

Keep the extracted folder together. Both executable projects reference the shared project and the central build configuration.

## Requirements

- .NET **10 SDK**.
- Azure CLI for live agent runs.
- An assigned Azure OpenAI resource, function-calling chat deployment and data-plane access.
- Windows PowerShell 5.1 or PowerShell 7 for the `.ps1` helper scripts.

Offline catalog and freshness checks require only the .NET SDK. They do not call a model or require Azure authentication.

## 1. Edit the common .env

From the extracted package root:

```powershell
if (-not (Test-Path .\.env)) { Copy-Item .\.env.example .\.env }
notepad .\.env
```

Replace the four `<assigned-...>` values with the assigned training configuration:

```dotenv
TENANT_ID=<assigned-tenant-id>
SUBSCRIPTION_ID=<assigned-subscription-id>
AZURE_OPENAI_ENDPOINT=https://<assigned-resource>.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT_NAME=<assigned-chat-deployment>
EVIDENCE_NOW_UTC=2026-10-30T12:00:00Z
```

There is **one active `.env`, at the root**. Both executable projects find it automatically, including when launched from their own directory or with `dotnet run --project` from elsewhere. No copy to the project directories is needed. `.env.example` is a reset template.

Existing process environment variables take precedence over `.env`. The loader supports `NAME=value`, blank lines, whole-line `#` comments, optional `export ` and matching single/double quotes. Values are literal: no shell expansion, escaping or inline comments. Do not add comments after a value.

The endpoint must be the Azure OpenAI resource endpoint. The deployment value is the **Azure deployment name**, not necessarily the underlying model name. The application uses `AzureCliCredential` and does not require an API key.

## 2. Restore and build

```powershell
dotnet restore .\Maf.Unit01.slnx --locked-mode -m:1
dotnet build .\Maf.Unit01.slnx --no-restore -m:1
```

`packages.lock.json` files are included for all three projects. Package versions are centralized at the root. `-m:1` builds the small solution sequentially. To intentionally change a package version, edit `Directory.Packages.props`, regenerate the locks with `dotnet restore .\Maf.Unit01.slnx --force-evaluate -m:1`, and build again.

## 3. Check the code offline

Check the original example's C# tool directly:

```powershell
dotnet run --project .\v01-native-integration\Unit01.Intro.csproj --no-build -- --catalog payroll
dotnet run --project .\v01-native-integration\Unit01.Intro.csproj --no-build -- --catalog vpn
dotnet run --project .\v01-native-integration\Unit01.Intro.csproj --no-build -- --catalog billing
```

Expected JSON statuses: `degraded`, `healthy` and `unknown`, respectively. Each call prints a `[tool] GetServiceStatus(...)` line. `--catalog` is a packaging convenience that invokes the function directly; it does not demonstrate model-driven tool selection.

## 4. Sign in for live agent runs

Load the same root settings into PowerShell for Azure CLI:

```powershell
. .\scripts\Load-Env.ps1
az login --tenant $env:TENANT_ID
az account set --subscription $env:SUBSCRIPTION_ID
```

The C# applications load `.env` themselves. Dot-sourcing the script is needed only to make its values available to commands such as `az login` in this shell.

## 5. Run the GitBook example

```powershell
.\run.ps1 v01 "What is the status of the payroll service?"
.\run.ps1 v01 "Is the VPN service healthy?"
.\run.ps1 v01 "What is the status of the billing service?"
```

Equivalent direct commands:

```powershell
dotnet run --project .\v01-native-integration\Unit01.Intro.csproj -- "What is the status of the payroll service?"
dotnet run --project .\v01-native-integration\Unit01.Intro.csproj -- "Is the VPN service healthy?"
dotnet run --project .\v01-native-integration\Unit01.Intro.csproj -- "What is the status of the billing service?"
```

| Request | Expected tool evidence | Expected answer |
| --- | --- | --- |
| Payroll | `payroll`, `degraded` | Explain the batch delay and avoid inventing a recovery time |
| VPN | `vpn`, `healthy` | Identify the synthetic catalog and report its observation |
| Billing | `billing`, `unknown` | Explain that the catalog has no matching entry |

Live runs should print a tool-call line before `Agent: ...`. Exact model wording can vary. The agent exposes a read-only tool; it cannot create tickets or restart services. Each live invocation has the same 45-second cancellation budget and 2,000-character input limit as the  example.

The reference answers in the solution README are illustrative expectations. They are not presented as captured Azure responses.

## Common errors

| Symptom | Check |
| --- | --- |
| Missing setting or placeholder error | Edit the root `.env`; check for an overriding process variable |
| 401 / credential error | Run `az login` with the assigned identity and tenant |
| 403 | Check assigned data-plane permissions and network access |
| 404 | Check the resource endpoint and deployment name |
| 429 | Check deployment capacity and follow its retry guidance |
| No tool-call line during a status request | Check function-calling support and inspect the agent behavior |
| Cannot locate the package marker | Keep `unit01-package.json`, the root configuration and project folders together |

All business records are synthetic. Live model requests use the configured Azure deployment and its quota.
