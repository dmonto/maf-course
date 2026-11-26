# Unit 16 — Migration to Microsoft Agent Framework in C#

All commands start at the **maf-course repository root**. This unit uses C#, .NET 10 and MAF 1.24.0. Each v01–v10 folder is an executable project. Shared code is in Shared/Unit16.Common; the local C# HTTP host is in Shared/Unit16.EvidenceHost. One .env in maf-unit16 is shared by every project. Environment variables already set in the process take precedence.

## Build and run

```powershell
if (-not (Test-Path .\maf-unit16\.env)) {
    Copy-Item .\maf-unit16\.env.example .\maf-unit16\.env
}
.\maf-unit16\Scripts\Build.ps1
.\maf-unit16\Scripts\Run-Example.ps1 -Example v01 -NoBuild
.\maf-unit16\Scripts\Test-All.ps1 -NoBuild
```

Scripts support Windows PowerShell 5.1 and PowerShell 7. They print the exact dotnet commands before launching them. Applications print the MAF calls, HTTP methods, request bodies, tool evidence, milestones, acceptance checks and conclusions. JSON reports are saved in maf-unit16/artifacts.

| Example | Observation |
| --- | --- |
| v01-architecture | Legacy recording and real MAF candidate preserve vpn/degraded |
| v02-prompts | Registered tool reference changes; user input keeps its role |
| v03-tools | Invalid booleans and service mismatches fail; MAF invokes the read tool once |
| v04-memory | Existing sessions remain on legacy; a fresh MAF agent restores a MAF session |
| v05-workflow | MAF graph emits workflow events and four distinct policy outcomes |
| v06-evaluation | Parity may be true while both implementations are wrong |
| v07-validation | Four cases pass; injected regression fails exactly the two VPN cases |
| v08-debt | Writer retirement follows draining; archive reader remains |
| v09-rollout | New reads return to legacy; operation and outbox identity remain stable |
| v10-best-practices | Contract success is separated from Azure qualification and retirement |

The legacy boundary is a synthetic recording, not an installed Semantic Kernel or AutoGen SDK. Default agent runs execute the **real MAF runtime and actual C# tool methods**, with a scripted IChatClient model transport. This proves deterministic integration, not model quality. No Azure inference occurs without -Live or an Azure-configured HTTP host.

## Prove regression detection

```powershell
.\maf-unit16\Scripts\Run-Example.ps1 -Example v07 -NoBuild -InjectRegression `
    -ExpectedExitCode 1 -Report .\maf-unit16\artifacts\regression.json
(Get-Content .\maf-unit16\artifacts\regression.json -Raw | ConvertFrom-Json).cases |
    Where-Object { -not $_.passed } | ConvertTo-Json -Depth 12
```

The application returns exit code 1; the wrapper verifies that this was expected. Exactly two cases must fail. Test-All.ps1 checks that count too.

## HTTP demonstration

In terminal A, from maf-course:

```powershell
.\maf-unit16\Scripts\Start-Host.ps1 -Mode fixture -NoBuild
```

In terminal B, also from maf-course:

```powershell
.\maf-unit16\Scripts\Test-Http.ps1 -BaseUrl 'http://127.0.0.1:8082'
.\maf-unit16\Scripts\Run-Example.ps1 -Example v07 -Http -NoBuild `
    -BaseUrl 'http://127.0.0.1:8082' -Report .\maf-unit16\artifacts\http.json
```

The console shows GET /health/ready, POST /api/status and their JSON payloads. The host prints the actual MAF execution. Unknown services return 400 before the agent runs. Stop the host with Ctrl+C.

The included host is local and uses AzureCliCredential in Azure mode. For the deployed Unit 14 managed-identity host, pass that host's actual base URL through -BaseUrl. A host without toolTrace is reported as missing tool evidence, never as zero calls.

## Azure qualification

Use the assigned Azure settings from the existing setup:

```powershell
. .\student.env.ps1
az login --tenant $env:TENANT_ID
if ($LASTEXITCODE -ne 0) { throw 'Azure sign-in failed.' }
.\maf-unit16\Scripts\Run-Example.ps1 -Example v07 -Live -NoBuild
```

Alternatively restart terminal A with Start-Host.ps1 -Mode azure -NoBuild and test with Run-Example.ps1 -Example v07 -Http -RequireAzure -NoBuild. Fixture HTTP responses cannot pass this gate. Credentials and token values are not printed.

## Instructor solutions

Solutions are separate executable C# projects under exercise-solutions. Run ex01 for payload-bound approval, expiry and revocation; run ex02 for a deterministic 10% canary with session-pinned rollback. They are not included in the GitBook exercise statements.

```powershell
.\maf-unit16\Scripts\Run-Example.ps1 -Example ex01 -NoBuild
.\maf-unit16\Scripts\Run-Example.ps1 -Example ex02 -NoBuild
```

GitBook.md is the aligned page source. Published page: https://diego-montoliu.gitbook.io/maf-course/session-7/unit-16
