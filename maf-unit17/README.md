# Unit 17 — Enterprise Use Cases with MAF: Internal Copilots, Automation and Complex Workflows

.NET 10, Windows PowerShell 5.1+, synthetic data. No production writes. From the maf-course root:

```powershell
.\maf-unit17\Scripts\Build.ps1
.\maf-unit17\Scripts\Test-All.ps1 -NoBuild
Set-Location .\maf-unit17
```

| Example | Official syllabus heading | Expected result |
|---|---|---|
| v01 | Internal support copilots | VPN suggests proposal; email no action; no ticket |
| v02 | Document automation | Valid invoice candidate accepted; invalid total rejected |
| v03 | Operations assistants | Two observations, VPN prioritized; no repair |
| v04 | Cross-department coordination | Two distinct reviewers, same evidence version; stale and duplicate actor rejected |
| v05 | Internal process automation | Persistent workflow across child processes; one ticket, blocked unauthorized writes |
| v06 | Integration with CRM or ERP systems | Actual loopback CRM GET and ERP POST twice; same draft receipt, one draft |
| v07 | Automated technical support | Approval required; exactly one simulated incident, replay returns same ID |
| v08 | Internal data analysis | Five synthetic rows, two correct completions, 40%, EUR 0.055 per correct ticket |
| v09 | Operational optimization | 900 minutes potential savings at 20% exception rate |
| v10 | Custom solution design | Coordination pattern plus rollout policy; paused writes preserve inspection |

Run each project with ` .\Scripts\Run-Example.ps1 -Example v01 -NoBuild` (replace the ID). Additional checks:

```powershell
.\Scripts\Run-Example.ps1 -Example v01 -NoBuild -ExampleArgs @('--service','email')
.\Scripts\Run-Example.ps1 -Example v02 -NoBuild -ExampleArgs @('--invalid')
.\Scripts\Run-Example.ps1 -Example v09 -NoBuild -ExampleArgs @('--exception-rate','0.4')
.\Scripts\Run-Example.ps1 -Example v10 -NoBuild -ExampleArgs @('--stage','pilot','--cohort','pilot','--stop-writes')
```

For a real MAF model call, keep Unit 14's Azure-mode service running on loopback port 8082 (configured in `.env` via `UNIT17_AZURE_BASE`), then run v01, v03, v05 or v07 with `-ExampleArgs @('--azure')`. The client performs GET `/health/ready` and POST `/api/status`; MAF executes on the server. A fixture-mode host on port 8080 may be selected with `-ExampleArgs @('--base','http://127.0.0.1:8080')`. No inference occurs without a configured host. v06 uses a separate real loopback HTTP server, with in-memory mock CRM/ERP data. The other examples demonstrate application contracts rather than model inference.

Persistent v05 CLI (fresh database; each invocation is a new process):

```powershell
$db = Join-Path $PWD ('state\incident-' + [guid]::NewGuid().ToString('N') + '.db')
dotnet run --project .\v05\v05.csproj --no-build -- --command start --db $db --case-id vpn-001
dotnet run --project .\v05\v05.csproj --no-build -- --command review --db $db --case-id vpn-001 --reviewer lab-reviewer --decision approve
dotnet run --project .\v05\v05.csproj --no-build -- --command execute --db $db --case-id vpn-001
dotnet run --project .\v05\v05.csproj --no-build -- --command inspect --db $db --case-id vpn-001
```

Exit codes: 0 success, 2 policy rejection, 1 failure. The shared `.env` is loaded by the examples; process environment wins. Do not reuse an incompatible SQLite schema. Reviewer identities and data are synthetic. `ExerciseSolutions/` contains design references for the two exercises.
