using Unit07.Common;

// Sequential orchestration: this project has its own executable entry point.
return await WorkflowHost.RunAsync(args, new("v06", "payroll", VerifyOrder: true));
