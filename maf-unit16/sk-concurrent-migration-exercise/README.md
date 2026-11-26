# Semantic Kernel concurrent workflow — source exercise

This is the **SK source application to migrate**, not the MAF solution. Read EXERCISE.md for the task and acceptance cases.

Requirements: .NET 10 SDK. No Azure account, model endpoint or environment file is required. Semantic Kernel Process.Core and Process.LocalRuntime are both pinned to 1.82.0-alpha. These are the SK Process Framework APIs; the concurrent block uses fan-out/fan-in routing.

## Run

From this directory:

```powershell
dotnet restore --locked-mode
dotnet run -- approved
dotnet run -- expired
dotnet run -- budget
dotnet run -- approved 1800 400
```

The supplied PowerShell launcher prints the exact commands before executing them:

```powershell
.\Run.ps1 -Outcome approved
.\Run.ps1 -Outcome expired
.\Run.ps1 -Outcome budget
.\Run.ps1 -Outcome approved -StatusMs 1800 -PolicyMs 400
```

The first argument selects the dummy review outcome. The next two optional arguments select the status and policy delays in milliseconds. Defaults: 1000 ms and 1500 ms.

## Read the code

* Program.cs: workflow construction, fan-out, fan-in, review routes and StartAsync.
* Workers.cs: fixed fixture values, artificial delays and console instrumentation.
* EXERCISE.md: migration requirements and acceptance cases, with no target implementation.

The two branches are activated by the same Start event. Each result targets a different required parameter of ValidateAsync. The local SK runtime waits until both parameters are available before invoking that function.

