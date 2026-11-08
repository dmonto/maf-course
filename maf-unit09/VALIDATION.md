# Validation — 6 November 2026

- .NET SDK 10.0.100: all 13 projects compiled with zero warnings and errors.
- 18 process checks passed: ten example defaults, both solutions, boundary verification, empty-scope/unsupported/payroll scenarios, an expected regression failure and a restored normal report.
- Verification project: 18 access, query, indexing and dependency-failure assertions passed against local HTTP doubles.
- PowerShell launchers executed successfully for v06 and ex01 using PowerShell 7. All three PowerShell scripts parsed successfully. Windows PowerShell 5.1 was not available for runtime verification.
- Live Azure chat, embeddings, Search, Blob access and classroom RBAC were not exercised.
- Original Markdown and code listings were captured from GitBook for traceability; dependencies and the fixture match the page.
