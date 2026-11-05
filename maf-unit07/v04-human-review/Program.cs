using Unit07.Common;

// Human-in-the-loop integration: this project has its own executable entry point.
return await WorkflowHost.RunAsync(args, new("v04", "payroll"));
