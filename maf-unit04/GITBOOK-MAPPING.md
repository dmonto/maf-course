# GitBook mapping

| Project | Current GitBook H2 | Executable example |
| --- | --- | --- |
| `v01` | Initial project configuration | Configuration-only host. /health reports agentRegistered=false; /api/chat is absent. No model or business lookup is performed. |
| `v02` | Model registration | Registers service-desk and resolves it at startup. /health reports the agent name and selected deployment. A capability-description request exercises the configured model; no business tool is registered yet. |
| `v03` | Tool definition | Registers GetServiceStatus. A payroll request returns degraded catalog evidence. CRM and billing use the same not-available public result. DevUI is introduced in v04. |
| `v04` | Basic execution | The complete HTTP and DevUI host, equivalent to the six-file GitBook application after shared-code extraction. A first request creates a conversation; a follow-up reuses its returned ID. |
| `v05` | Observability with DevUI | The full host, with a protocol request script and a manual DevUI inspection. Observe ToolCompleted, RequestCompleted and SpanCompleted. Protocol conversations and /api/chat conversations remain separate. |
| `v06` | Structured logging | The full host, tested with CRM. Expect a completed HTTP turn with ok=false, code=not-available and no data. Correlate request and tool logs by RunId. |
| `v07` | Prompt management | The full host with the shared v1 and v2 instruction files. Start with -PromptVersion v2 to load the candidate. /health and scopes report v2. Restart to load changes. The candidate adds a Catalog status: prefix after successful lookups; demo mode does not evaluate prompt quality. |
| `v08` | Basic state management | The full host, tested with a payroll follow-up and a separate fresh conversation. Limits: six completed turns, twenty retained API conversations, 409 for an overlapping turn, and invalidation after a failed run. No idle expiry is added here. |
| `v09` | Initial testing | The full host and repeatable checks: readiness, invalid input (400), unknown conversation (404), and a completed payroll turn with degraded evidence. |
| `v10` | Behavior validation | The full host and eight acceptance cases: payroll, VPN, missing subject, CRM, billing, unsupported action, conflicting instruction and a follow-up. Machine assertions inspect evidence; meaning and unsupported claims still require review. |


## Refactoring notes

The topic's source is a single complete application spread across several sections. The ZIP provides one executable project per section, including narrative examples that contain no code block. v01–v03 expose deliberate configuration/registration/tool milestones; v04–v10 reuse the complete application and provide distinct section-specific tests.

The shared ServiceTools catalog, result codes, request evidence, Azure CLI credential, agent name and baseline session rules match the topic. AgentChat is extracted with unchanged default limits (45 seconds, six turns and twenty API sessions). The root .env replaces manually sourcing student.env.ps1; it preserves the assigned AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT_NAME variable names. Prompt and service/model names retain separate roles.

The root v2 prompt implements the topic's requested one-change candidate. No exercise solution is used by v01–v10. Exercise 2's alternate handler exists only under Exercises and is selected only by ex02. Both HTTP entry points use the registered agent; custom API limits do not automatically apply to DevUI's protocol conversation store.

Reference/GitBook-original contains exact extracted executable blocks for comparison; it is excluded from the solution. This task does not modify GitBook.
