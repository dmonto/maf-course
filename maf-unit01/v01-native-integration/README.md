# v01 - Read-only service-status agent

**GitBook section:** Native integration with .NET and Python (section 7).

`Program.cs` registers the status function and starts the common runner. `Instructions.cs` contains the original instructions. The shared project holds the catalog, tool implementation, environment loader and execution code that remain reusable.

From this directory, after restoring/building the root solution:

```powershell
dotnet run -- "What is the status of the payroll service?"
dotnet run -- "Is the VPN service healthy?"
dotnet run -- "What is the status of the billing service?"
```

The root `.env` is found automatically. Sign in with Azure CLI before live runs.

To inspect a function result without Azure:

```powershell
dotnet run -- --catalog payroll
dotnet run -- --catalog billing
```

Expected results: payroll is `degraded`; billing is `unknown`. The JSON has the same `service`, `status`, `detail` and `source` fields as GitBook. Freshness handling belongs to the separate exercise solution and is not implemented here.
