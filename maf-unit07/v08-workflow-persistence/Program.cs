using Unit07.Common;

// Workflow persistence: this project has its own executable entry point.
return await WorkflowHost.RunAsync(args, new("v08", "payroll", "pause"));
