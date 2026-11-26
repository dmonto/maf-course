using Unit14;

if (args is ["--help"])
{
    Console.WriteLine("dotnet run --project Shared/Unit14.Probe -- --release VERSION [--base ORIGIN] [--mode fixture|azure] [--requests 1..20] [--parallel 1..4] [--timeout 1..300] [--output PATH]");
    return 0;
}
try
{
    return (await DeploymentProbe.RunAsync(ProbeOptions.Parse(args))).ExitCode;
}
catch (ArgumentException error)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}
catch (Exception error)
{
    Console.Error.WriteLine($"Probe could not produce a report: {error.GetType().Name}");
    return 1;
}
