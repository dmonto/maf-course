# Unit 12 source mapping

Source page: `5NyZDQFujwe6GeJ3CBCA`, page ID `OyX1sT2tus9A6SrrWJ5T`, path `session-6/unit-12`, read on 7 November 2026.

| Syllabus H2 | Example in current GitBook | Executable project |
| --- | --- | --- |
| Automated response evaluation | Prepare Unit12.Evaluation, cases and recorded tool | v01 + shared contract/tool/dataset |
| Quality metrics | Distinguish scores from valid evaluations | v02; validator inputs explicitly synthetic; Azure judges in shared runner |
| Regression testing | Positive and negative fixture checks | v03 |
| Comparative model evaluation | Paired assigned-deployment comparison | v04; fixture mechanics demonstration and live A/B mode |
| Tool validation | Inspect actual tool evidence | v05 |
| Drift control | Diagnose a drift signal (narrative example) | v06; evidence/configuration diagnostics, no statistical detector |
| Testing automation | Complete application runner | v07 + shared MAF and reporting implementation |
| Integration into CI/CD | Complete fixture workflow | v08 + PowerShell process gate + workflow template |
| Result analysis | Generate and inspect quality report | v09 evidence analysis + New-QualityReport.ps1 / pinned aieval tool |

## Implementation mapping

| Original listing | Pack equivalent |
| --- | --- |
| `Unit12.Evaluation.csproj` | Shared/Unit12.Common/Unit12.Common.csproj with the same six pinned package versions; thin executable projects reference it |
| `Cases.cs` | Shared/Unit12.Common/Contracts.cs |
| `Program.cs` | Shared/Unit12.Common/EvaluationRunner.cs, EnvironmentConfig.cs and Reports.cs; v07 entry point |
| `unit12-evaluation.yml` | Unit-local .github/workflows/unit12-evaluation.yml template using maf-unit12 paths and complete solution |

The source's .NET setup takes precedence over the generic earlier Python authoring guidance. Package versions match the current source. The syllabus's plural `Evaluations` refers to the singular `Microsoft.Extensions.AI.Evaluation` namespace and package family.

Source behavior retained: four synthetic cases, fresh tools and agent conversation per sample, explicit false notification flag, matching read trace, frozen expectations withheld from the assistant, separate relevance/grounding judge, threshold 4, invalid score handling, 1–5 repetitions, two-minute sample budget, three function-invocation iterations, uncached judgments and retained reports.

`Reference/OriginalListing` retains the unmodified original code and workflow for traceability. It is not included in the solution; use the executable projects above. GitBook and earlier units were not edited.
