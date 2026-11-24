# Validation record

Validated on 8 November 2026 using .NET SDK 10.0.100, PowerShell 7.4.6 and Python on Linux.

- The complete solution restored with its pinned dependencies and compiled with zero warnings and zero errors.
- All ten section entry projects passed in fixture mode.
- The additional .NET boundary checks passed: malformed tool arguments, changed bound identity, audit-resource sanitization, literal environment values, environment precedence and retention path scope.
- The original GitBook Python gate accepted real .NET-generated seven-case evidence.
- Five independent mutations were rejected: missing case, unauthorized allow, sensitive audit field, changed policy and absent audit events.
- The PowerShell example wrapper successfully passed an artifact directory containing spaces to the Python gate from an unrelated working directory.
- The pipeline command completed locked restore, build and the combined fixture suite/gate successfully.

The live Azure MAF path was compiled. It was not executed against an assigned Azure deployment during packaging. Configure the shared `.env` and run v04 or v10 in Azure mode in the assigned environment.

The package does not claim validation on a Windows host, a deployed user-authentication boundary, configured Azure network rules or Microsoft Purview enforcement. Those require the corresponding environment and integration tests.
