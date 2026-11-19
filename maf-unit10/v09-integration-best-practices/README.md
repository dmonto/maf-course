# Integration best practices

Run the integration walkthrough or the local acceptance matrix from the concluding section.

Run from the maf-unit10 root:

```powershell
./Scripts/Run-Example.ps1 -Example v09 -Demo -Case acceptance
```

Expected: The acceptance case passes seven checks and writes reports/integration-acceptance.json.

The significant implementation is in `Program.cs, Shared/AgentHost.cs and Shared/NotificationFlow.cs`. `Program.cs` is this project's independent entry point. The one common `.env` and shared source directories remain at the unit root.

```powershell
./Scripts/Run-Example.ps1 -Example v09 -Demo -Agent
./Scripts/Run-Example.ps1 -Example v09 -Demo -Send -Approve -Operation end-to-end-001
```

The -Agent switch runs the real function wrappers in local demo mode and Azure-backed MAF inference with -Live. The default walkthrough prepares only a preview.
