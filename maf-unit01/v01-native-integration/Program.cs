using MafCourse.Unit01.Intro;
using MafCourse.Unit01.Shared;
using Microsoft.Extensions.AI;

// This local-only option checks the function without authenticating or calling a model.
if (args.Length > 0 && args[0] == "--catalog")
{
    if (args.Length > 2)
    {
        Console.Error.WriteLine("Usage: --catalog [service-name]");
        return 1;
    }
    Console.WriteLine(ServiceTools.GetServiceStatus(args.ElementAtOrDefault(1) ?? "payroll"));
    return 0;
}

return await AgentConsole.RunAsync(
    args,
    Instructions.Text,
    AIFunctionFactory.Create(ServiceTools.GetServiceStatus));
