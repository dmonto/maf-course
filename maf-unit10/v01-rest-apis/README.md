# Connection to REST APIs

Run the registered MAF read functions. Demo mode invokes them explicitly without a model; live mode runs the MAF agent.

Run from the maf-unit10 root:

```powershell
./Scripts/Run-Example.ps1 -Example v01 -Demo
```

Expected: Degraded VPN status, 2 open tickets, two function schemas with only the service argument.

The significant implementation is in `Shared/AgentHost.cs and Shared/Connectors.cs`. `Program.cs` is this project's independent entry point. The one common `.env` and shared source directories remain at the unit root.
