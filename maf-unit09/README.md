# Unit 9 — executable examples (.NET 10)

One independent console project for every GitBook section containing an example, in syllabus order. `Shared/`, `Knowledge/`, `Prompts/` and one root `.env` are reused by all projects. Source comments, instructions and output are in English.

Source: [current Unit 9](https://app.gitbook.com/s/5NyZDQFujwe6GeJ3CBCA/session-4/unit-9), read on 6 November 2026. The full source snapshot and original listings are in `Reference/`.

## Quick start

From the repository root:

```powershell
Set-Location .\maf-unit09
Copy-Item .env.example .env
# Demo runs need no credentials or Azure resources.
.\Scripts\Run-Example.ps1 -Example v01 -Demo
.\Scripts\Run-Example.ps1 -Example v03 -Demo
.\Scripts\Test-Examples.ps1
```

Each directory can also run directly, from any working directory:

```powershell
dotnet run --project .\v04-contextual-retrieval\Unit09.V04.csproj -- --demo --profile none
```

Direct `dotnet run` calls accept the corresponding lowercase `--profile`, `--question`, `--mode`, `--case`, `--version`, `--service` and `--force-failure` arguments. Use the arguments documented for the selected project below. Do not move an example directory away from its unit root; the shared files are part of the package.

## Projects and syllabus mapping

| ID | GitBook section | Directory |
| --- | --- | --- |
| `v01` | RAG fundamentals | `v01-rag-fundamentals` |
| `v02` | Document indexing | `v02-document-indexing` |
| `v03` | Integration with vector databases | `v03-vector-database` |
| `v04` | Contextual retrieval | `v04-contextual-retrieval` |
| `v05` | Secure document access | `v05-secure-document-access` |
| `v06` | Permission control | `v06-permission-control` |
| `v07` | Index updates | `v07-index-updates` |
| `v08` | Accuracy evaluation | `v08-accuracy-evaluation` |
| `v09` | Integration with corporate storage | `v09-corporate-storage` |
| `v10` | Best practices for enterprise environments | `v10-enterprise-pattern` |

The two blue examples in Document indexing share v02. Concept-only blue examples in RAG fundamentals, Permission control, Accuracy evaluation and Best practices also have executable projects. 

## Common configuration and Azure execution

Edit only `maf-unit09/.env`. Do not create separate copies in example directories. The C# loader locates the unit root from the executable directory, accepts simple `KEY=value` assignments and quoted values, and gives nonempty process environment variables precedence. It does not execute scripts, expand variables or contact a secret store. `.env` is ignored by Git; the committed template contains no credentials. Existing `student.env.ps1` settings can also be loaded into the launching shell.

Use the assigned resource-root OpenAI endpoint, separate chat and embedding deployment names, Search endpoint and index. The embedding dimensions must match the assigned deployment's actual output. The template defaults to 1536 for the established text-embedding-3-small deployment.

```powershell
# Run after filling the common .env.
. .\Scripts\Import-Env.ps1
az login --tenant $env:TENANT_ID
if ($LASTEXITCODE -ne 0) { throw 'Azure login failed.' }
az account set --subscription $env:SUBSCRIPTION_ID
if ($LASTEXITCODE -ne 0) { throw 'Subscription selection failed.' }

# An authorized index writer runs this once. It writes the assigned index.
.\Scripts\Run-Example.ps1 -Example v02 -Live
# Allow indexing to become searchable, then run read examples.
.\Scripts\Run-Example.ps1 -Example v03 -Live
.\Scripts\Run-Example.ps1 -Example v04 -Live -Question 'What should I collect for Windows 11 VPN error 809?'
```

| Operation | Required training-resource role |
| --- | --- |
| Chat and embeddings | Cognitive Services OpenAI User |
| Create/update Search schema | Search Service Contributor |
| Upload chunks | Search Index Data Contributor |
| Search documents | Search Index Data Reader |
| Download Blob fixture | Storage Blob Data Reader |


## What to run and what to expect

| Run from the unit root | Expected observation |
| --- | --- |
| `./Scripts/Run-Example.ps1 -Example v01 -Demo` | VPN evidence, source IDs and a deterministic supported response |
| `./Scripts/Run-Example.ps1 -Example v02 -Demo` | Four source documents, five chunks and successful per-item upload checks |
| `./Scripts/Run-Example.ps1 -Example v03 -Demo` | Keyword, vector and hybrid evidence for the same question |
| `./Scripts/Run-Example.ps1 -Example v04 -Demo` | Recovery time is unavailable in the guide |
| `./Scripts/Run-Example.ps1 -Example v04 -Demo -Profile none` | Zero evidence; no chat inference |
| `./Scripts/Run-Example.ps1 -Example v05 -Demo` | Payroll and cross-tenant isolation checks pass |
| `./Scripts/Run-Example.ps1 -Example v06 -Demo` | Actual AIFunction schema exposes only query, with no tenant/group argument |
| `./Scripts/Run-Example.ps1 -Example v07 -Demo` | Active v1 and staged v2 are queried independently; promoted is false |
| `./Scripts/Run-Example.ps1 -Example v08 -Demo -Case unsupported` | Evidence and answer are displayed for manual groundedness review |
| `./Scripts/Run-Example.ps1 -Example v08 -Demo -Case payroll-denied` | Restricted payroll text is absent |
| `./Scripts/Run-Example.ps1 -Example v09 -Demo` | Local copy of the five-file Blob fixture is parsed; no index write |
| `./Scripts/Run-Example.ps1 -Example v10 -Demo -Service payroll -Profile payroll` | Synthetic degraded status is separate from guide evidence; human review required; no action executed |
| `./Scripts/Run-Example.ps1 -Example verify -Demo` | Dependency-failure, filter and indexing boundary checks pass |

## Blob fixture

V09 uses the existing manifest/source-file ingestion boundary. In live mode the assigned training container must already contain `unit09/knowledge.json`, `unit09/vpn-guide.md`, `unit09/payroll-guide.md`, `unit09/password-guide.md` and `unit09/tenant-b-vpn.md`. The downloader uses Azure CLI identity, a Storage bearer token and HTTPS Blob REST requests; it writes to ignored `data/blob-fixture/`. It does not upload, index or publish the downloaded files. The local demonstration copies the exact fixture instead. No signed URLs or storage tokens enter prompts or output.

## Shared implementation and differences from the page

The authoritative `RagTypes.cs`, `SearchStore.cs`, complete original `Program.cs`, project dependencies, knowledge files and instruction text were recovered from the current GitBook page. The dependency versions remain Azure.AI.OpenAI 2.9.0-beta.1, Azure.Identity 1.21.0 and Microsoft.Agents.AI.OpenAI 1.24.0.

`Reference/` keeps the page snapshot and original source listings as text for comparison. They are not extra projects and are not compiled into examples. `Prompts/knowledge.txt` retains the page's grounding instructions.

## Local demonstration limits and validation

The demo runs the same SearchStore code through an in-memory HTTP handler with the real fixture, filters and version checks. It uses deterministic token-hash vectors with a small vocabulary mapping, lexical overlap and illustrative rank fusion. It does not reproduce Azure ANN, BM25 or semantic embeddings, and its rankings are not a cloud quality benchmark. Demo answers display evidence deterministically and are labelled with modelInvoked=false. Live answers use the real MAF agent and configured deployment. The service-status catalog in v10 is synthetic in both modes.

Build and local executions are covered by `Scripts/Test-Examples.ps1`; `Verification/` checks meaningful access and failure boundaries. Azure inference, RBAC, indexing propagation and Blob connectivity require the assigned classroom resources and were not tested using production credentials. PowerShell scripts target the Windows course environment; direct dotnet commands also work on Linux.

Official Blob contract references: [Bearer authorization](https://learn.microsoft.com/en-us/rest/api/storageservices/authorize-with-azure-active-directory), [Get Blob](https://learn.microsoft.com/en-us/rest/api/storageservices/get-blob).
