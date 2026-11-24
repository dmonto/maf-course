# Validation

Validated on 7 November 2026 with .NET SDK 10.0.100 and PowerShell on Linux.

| Check | Observed result |
| --- | --- |
| Locked solution restore | Passed with all 13 lock files present |
| Complete solution build | 13 projects compiled; 0 warnings and 0 errors |
| Nine example programs through the PowerShell launcher | All expected exit codes observed, including deliberate rejections in v04 and v06 |
| Verification executable | 43 checks passed |
| MAF function wiring | Real ChatClientAgent / FunctionInvokingChatClient invokes the registered read tool through a scripted local IChatClient; two model-transport turns and a valid recorded trace |
| Actual CLI regression gate | Positive process returns 0; known bad process returns 1 |
| Repetition boundary | 5 repetitions produce 20 isolated samples; 0 is rejected with exit 2 |
| Invalid/missing configuration | Unknown or duplicate mode and missing Azure settings return 2 |
| Environment behavior | Initializer preserves an existing .env; process settings override file defaults; executables work from another current directory |
| Reporting utility | Pinned aieval 10.10.0 restores and its CLI starts successfully |
| PowerShell files | All scripts parse successfully |
| Local evidence HTML | Written from run JSON and marked explicitly when semantic judges were not run |

The verification suite covers malformed JSON, missing notification flag, duplicate properties, wrong status, wrong/missing/repeated tool calls, changed tool evidence, denial versus missing evidence, invalid numeric metrics, diagnostics, missing judgments, critical invariants despite high quality scores, low valid scores, infrastructure errors, report retention, incompatible comparison inputs, evidence-based drift diagnosis and MAF tool wiring.

The shipped environment contains no Azure assignments or credentials. Live Azure assistant inference, judge compatibility and judge scores were not tested. No model ranking or semantic quality result is claimed. The scripted MAF test verifies framework wiring only. The aieval CLI was verified; generating its semantic HTML still requires a reporting-library scenario store from a live Azure run. The separate local HTML is the deterministic evidence report.

The GitHub Actions workflow is a template inside the unit. It was not installed at repository root or executed on GitHub. GitBook and all earlier units were left unchanged. Generated binaries, object directories and test artifacts are excluded from the source ZIP.

Reproduce locally after build:

```powershell
.\Scripts\Test-All.ps1 -NoBuild
```
