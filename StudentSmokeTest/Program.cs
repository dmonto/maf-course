using Azure.Identity;
using StudentSmokeTest;

if (args.SequenceEqual(new[] { "--self-test" })) return await SelfTests.RunAsync();
if (args.Length > 0)
{
    Console.WriteLine("Usage: dotnet run --project StudentSmokeTest [-- --self-test]");
    return 2;
}

Console.WriteLine("MAF course | .NET 10 student smoke test");
Console.WriteLine("Small model calls may incur Azure charges. Write tests are disabled unless SMOKE_WRITE_TESTS=1.");
Config config;
try { config = Config.Load(); }
catch (CheckException ex)
{
    Console.WriteLine($"[FAIL] Configuration: {ex.Message}");
    Console.WriteLine("FINAL RESULT: ACTION REQUIRED");
    return 2;
}

using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
var credential = new AzureCliCredential(new AzureCliCredentialOptions { TenantId = config.TenantId });
var runner = new Runner();
await new Checks(config, credential, http).ExecuteAsync(runner);
try { await runner.SaveAsync(Path.Combine(Directory.GetCurrentDirectory(), "reports")); }
catch (IOException) { Console.WriteLine("Report could not be saved; copy the PASS/FAIL lines for the coordinator."); }
catch (UnauthorizedAccessException) { Console.WriteLine("Report folder is not writable; copy the PASS/FAIL lines for the coordinator."); }

Console.WriteLine(runner.Passed ? "FINAL RESULT: READY FOR CORE LABS" : "FINAL RESULT: ACTION REQUIRED");
Console.WriteLine("Scope: cloud access and core agent/RAG prerequisites. Complete the manual checklist for SharePoint, Graph, collaboration and later labs.");
if (!runner.Passed) Console.WriteLine("If any check fails, send the report to the course coordinator. Do not send tokens or secrets.");
return runner.Passed ? 0 : 1;
