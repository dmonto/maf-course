# MAF course — student checklist and smoke test (.NET 10)

## 1. Clone the repository

In PowerShell:

```powershell
git clone https://github.com/dmonto/maf-course.git
cd maf-course
```

## 2. Check your workstation and accounts

- [ ] .NET **10 SDK**, compatible Visual Studio or VS Code, Git and Azure CLI installed.
- [ ] **32 GB RAM** and **50 GB free disk space**, as specified in the syllabus.
- [ ] Assigned corporate account and working MFA; access to Azure Portal and Foundry.
- [ ] Access to the lab SharePoint site, course Slack and assigned Azure DevOps organization.
- [ ] Coordinator's assigned endpoints, deployments, Search index and Blob container. For Graph labs, verify site API access through the assigned app/sign-in flow; browser access alone does not verify Graph consent.

## 3. Load your assigned settings

```powershell
Copy-Item .\student.env.example.ps1 .\student.env.ps1
notepad .\student.env.ps1
# Replace every <placeholder>, save, then load into this PowerShell session:
. .\student.env.ps1
```

Use assigned **deployment names**, not model names. The index and container must exist. Keep `SMOKE_WRITE_TESTS="0"` initially. Never commit credentials; personal settings and reports are excluded from Git.

## 4. Sign in to Azure

```powershell
az login --tenant $env:TENANT_ID
az account set --subscription $env:SUBSCRIPTION_ID
```

Use your assigned student account. If needed, add `--use-device-code` only when corporate policy permits it.

## 5. Restore and run

```powershell
dotnet restore .\StudentSmokeTest\StudentSmokeTest.csproj --locked-mode
.\run-smoke.ps1
```

Use the organization's approved PowerShell execution policy. Results are `PASS`, `FAIL` or `SKIP`; a JSON report is saved in `reports/`.

Expected final line:

```text
FINAL RESULT: READY FOR CORE LABS
```

Checks: local tools/context, resource group, Foundry, **MAF function calling**, Responses, embeddings, Search **document queries** and Blob listing. Small model calls incur Azure charges. Later labs, Graph consent and skipped write checks require separate validation.

## 6. Optional lab checks

If the coordinator has assigned the required write roles:

```powershell
$env:SMOKE_WRITE_TESTS = "1"
.\run-smoke.ps1
```

Creates, verifies and deletes a synthetic blob and temporary Search index. Cleanup failures name the remaining item.

Docker, Podman, Ollama and Foundry Local are not required or checked by this smoke test. Container and local-model labs are validated separately.

## Quick error guide

| Symptom | What to do |
|---|---|
| Missing tool | Install the .NET 10 **SDK**, Git and Azure CLI; check PATH/permissions. |
| Wrong context/login/401 | Repeat step 4; check tenant, MFA and Conditional Access. |
| 403 | Administrator: check data-plane roles/scope, role propagation and firewall. |
| 404 | Check project, deployment, index and container names. |
| 400 / tool not called | Coordinator: check deployment capabilities/configuration. |
| 429 | Wait; coordinator: check quota/capacity. |
| NuGet/network/TLS | Check proxy, certificates and HTTPS access to Azure/NuGet. |
| Cleanup failure | Coordinator: remove the named temporary item. |

**If any check fails, please notify the course coordinator and send the report or PASS/FAIL lines. Never send tokens or secrets.**

Coordinator: upload the ZIP **contents** to the root of `dmonto/maf-course`. See [TECHNICAL_NOTES.md](TECHNICAL_NOTES.md) for scope and offline validation.
