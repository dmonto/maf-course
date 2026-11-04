# MAF Unit 4: example projects

Your First Agent with MAF in .NET: Configuration, Models and DevUI Observability.

Ten executable projects map to the ten GitBook sections, including examples described without a code block. Shared implementation and configuration live at the package root. Two possible exercise solutions live under `Exercises`.

## Prerequisites and checkout

Use the .NET 10 SDK, PowerShell, Git and access to NuGet prerelease packages. Live Azure mode also requires Azure CLI, the assigned resource-root endpoint, deployment and model-access role.

If the course repository has not been cloned:

```powershell
git clone https://github.com/dmonto/maf-course.git
if ($LASTEXITCODE -ne 0) { throw 'Repository clone failed.' }
Set-Location ./maf-course
```

Extract this ZIP into that repository, preserving its complete `maf-unit04-examples` directory. Then enter the extracted directory:

```powershell
Set-Location ./maf-unit04-examples
dotnet --version
dotnet build ./Maf.Unit04.slnx -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
```

The ZIP includes a placeholder-only root `.env` and an identical `.env.example`. Edit the root `.env`; do not create per-project copies. If you are preparing a fresh configuration later:

```powershell
Copy-Item ./.env.example ./.env
```

This command replaces the existing file, so use it only when intentionally resetting your local configuration. The root `.gitignore` excludes `.env`, generated build files and run output. Commit `.env.example` and dependency lock files when copying the examples into Git.

## Quick start

Terminal 1, from the package root:

```powershell
& ./Scripts/Run-Example.ps1 -Example v04 -Demo
```

Terminal 2:

```powershell
& ./Scripts/Test-Example.ps1 -Example v04
```

Open `http://127.0.0.1:5050/devui`, select `service-desk`, and ask about payroll or VPN. The demo client supports streaming so the actual DevUI and protocol endpoint can exercise the MAF tool/session pipeline. All demo inference is scripted and synthetic; it neither contacts Azure nor measures model quality or token costs.

Stop a running host with Ctrl+C before launching another project on the default port. Each example has its own README with commands and expected observations. A non-default port can be selected consistently:

```powershell
& ./Scripts/Run-Example.ps1 -Example v06 -Demo -Port 5051
# In another terminal:
& ./Scripts/Test-Example.ps1 -Example v06 -BaseUrl http://127.0.0.1:5051
```

## Live Azure mode

Set these values in the one shared `.env`:

```dotenv
MAF_EXECUTION_MODE=live
AZURE_OPENAI_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT_NAME=YOUR-ASSIGNED-DEPLOYMENT
TENANT_ID=YOUR-TENANT-ID
SUBSCRIPTION_ID=YOUR-SUBSCRIPTION-ID
```

Use the HTTPS resource root for AzureOpenAIClient. A Foundry project path and an `/openai/v1/` base URL are different client configurations. The deployment must support Chat Completions and function tools. Azure CLI authenticates model access; the tool's fixed synthetic caller scope remains an application concern.

```powershell
& ./Scripts/Login-Azure.ps1
& ./Scripts/Run-Example.ps1 -Example v04 -Live
```

In another terminal, run the same section test. Health proves local readiness; a live grounded chat additionally exercises the selected Azure deployment. Failed live execution never falls back to the scripted client.

The application loads `.env` automatically from the directory containing `Maf.Unit04.slnx`, searching ancestors of the current directory and executable directory. Nonempty process environment variables take precedence, so a value previously imported into a terminal can override later `.env` edits. To use a changed `.env` value, clear the corresponding process variable or start a clean terminal. `Scripts/Import-Env.ps1` imports the same simple NAME=value syntax when a PowerShell command needs those settings. It does not execute the file or expand shell expressions.

## Example map

| ID | Section | Main observation |
| --- | --- | --- |
| v01 | Initial project configuration | Configuration-only health; no registered agent |
| v02 | Model registration | Named agent, deployment and prompt roles; no business tool yet |
| v03 | Tool definition | Read-only catalog tool with permitted evidence |
| v04 | Basic execution | Complete application API and DevUI host |
| v05 | Observability with DevUI | Protocol request, DevUI inspection and trace summaries |
| v06 | Structured logging | Completed CRM refusal correlated by RunId |
| v07 | Prompt management | Select v1/v2 at startup and repeat inputs |
| v08 | Basic state management | Follow-up continuity, fresh subject and API limits |
| v09 | Initial testing | Health, 400, 404 and grounded payroll evidence |
| v10 | Behavior validation | Eight acceptance cases and semantic review |

Run `Scripts/Test-Example.ps1 -Example vXX` after starting the matching project. That check refuses to test the wrong running project. v04–v10 intentionally share the complete implementation; their distinct project entry points and test branches correspond to each section's example rather than duplicating the host.

The custom API retains at most twenty conversations and accepts at most six completed turns per session by default. Concurrent turns for one session return 409; unrelated sessions can proceed. An interrupted or failed turn invalidates its conversation. DevUI uses separate protocol conversations; the custom API's limits are not automatically applied to that store. The entire host is bound to loopback and uses a fixed synthetic caller for this local example.

## Shared files

- `.env`, `.env.example`: the one configuration source for all projects.
- `appsettings.json`: logging and application defaults.
- `Directory.Build.props`, `Directory.Packages.props`: .NET defaults and pinned dependencies.
- `Prompts`: shared instruction versions.
- `Shared/Unit04.Common`: host, catalog tool, API session handler, telemetry and configuration.
- `Scripts`: example launcher, HTTP clients, login and verification commands.
- `Reference/GitBook-original`: exact executable blocks extracted from the current page; not part of the build.
- `GITBOOK-MAPPING.md`: section mapping and explicit refactoring notes.

Package versions match the topic: Azure.AI.OpenAI 2.9.0-beta.1, Azure.Identity 1.21.0, Microsoft.Agents.AI.OpenAI 1.24.0, Hosting/DevUI 1.24.0-preview.261006.1 and Hosting.OpenAI 1.24.0-alpha.261006.1. Keep generated `packages.lock.json` files. To verify the retained dependency graph later, run:

```powershell
dotnet restore ./Maf.Unit04.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
```

## Possible exercise solutions

Print the reference diagnosis for Exercise 1:

```powershell
& ./Scripts/Run-Example.ps1 -Example ex01
```

Start the separate expiry solution for Exercise 2:

```powershell
& ./Scripts/Run-Example.ps1 -Example ex02 -Demo
```

Then, in another terminal:

```powershell
& ./Exercises/ex02-expiring-conversations/Test-Expiry.ps1
```

The solution uses `CONVERSATION_IDLE_SECONDS=60`. Its test waits beyond that interval, demonstrates HTTP 410 and creates a new conversation. The ordinary examples have no idle-expiry solution. See each exercise README for the reasoning, race-prevention mechanism and bounded expired-ID policy.

## Verification

```powershell
& ./Scripts/Test-Offline.ps1
```

This uses a scripted client inside the real MAF pipeline and actual local HTTP hosts. It checks section profiles, tool evidence, API validation, sessions, concurrency, protocol responses, streaming, capacity and expiry. It does not establish live Azure authentication or assess a real model's answer quality. See `VALIDATION.md` for the checks performed when preparing this ZIP.

The v10 script saves case results under `output/v10-results.json`. Review whether every factual claim is supported, recovery times are not invented, and unavailable information is explained correctly. A successful HTTP response or a successful tool lookup alone does not establish answer quality.
