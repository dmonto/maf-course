using Unit07.Common;

// Definition of task graphs: this project has its own executable entry point.
return await WorkflowHost.RunAsync(args, new("v01", PrintGraph: true));
