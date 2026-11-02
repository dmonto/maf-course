# Validation record

Source snapshot and pack validation date: 2026-10-30.

| Check | Result |
| --- | --- |
| All 12 .NET projects, including 8 examples and 2 solutions | Build passed, zero warnings and zero errors |
| SDK used | .NET 10.0.402 on Linux |
| Locked restore | Passed against all included packages.lock.json files |
| Independent policy, tool and session checks | 36 passed, using real MAF classes and a labelled scripted model |
| Eight example entry points | All passed in demo mode through the PowerShell launcher |
| Both possible exercise solutions | Passed in local/demo mode |
| Alternative repository injection | Passed |
| PowerShell syntax | All four scripts parsed successfully in PowerShell 7.5.2 |
| Shared environment import | Executed successfully against the root template |
| Blue-example coverage | Exact match to the 8 sections containing blue examples in the current GitBook page |
| Configuration layout | Exactly one active .env, at the package root |
| Published code reference | Exact C# block extraction retained in Reference/GitBook.Program.cs.txt |

The checks cover model-visible tool schema, normalized arguments, denied and missing records, validation before repository access, cancellation, caller isolation, real MAF function invocation, session continuity/reset, input and turn limits, interruption before/after tool execution, timeout, empty output, loop termination, shared status/contact budgets, contact tool selection, ticket ownership, exact one-use approvals and confirmed ticket closure.

The offline PowerShell runner was executed with `-SkipBuild` after the complete solution had already been built. Locked restore was checked separately. The synthetic model double now respects both explicit no-tools mode and removal of invocable tools at the final iteration.

Live Azure inference and interactive Azure CLI sign-in were not executed. They require the assigned tenant, subscription, data-plane access and a deployment supporting Chat Completions/function calling. No Azure inference request or real corporate-system write was made during this validation. Demo timings and request counts describe local execution only.

To reproduce after extracting:

```powershell
& ./Scripts/Test-Offline.ps1
```
