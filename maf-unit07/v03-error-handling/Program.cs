using Unit07.Common;

// Error handling in workflows: this project has its own executable entry point.
return await WorkflowHost.RunAsync(args, new("v03", "payroll"));
