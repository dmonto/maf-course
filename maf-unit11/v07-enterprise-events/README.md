# Enterprise use cases

Run from the unit root:

```powershell
.\Scripts\Run-Example.ps1 -Example v07 -Demo
```

Expected: A durable event is explained and its final result committed. actionExecuted=false.

Use -Live for Azure MAF; -Command serve runs port 5117. Optional -Command bus and -Command bus-send use the assigned real Service Bus queue.

Shared code: `../Shared/Unit11.Common`. Edit only `../.env`; keep this directory inside the complete package. See the root README for installation, direct dotnet commands and mode details.
