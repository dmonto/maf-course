using Unit02.Common;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
Check(Costs.Estimate(1500, 250, .40m, 1.60m) == .001m, "GitBook per-call cost");
Check(Costs.Estimate(400, 100, 1m, 4m) * 9000 == 7.20m, "Exercise 2 counts all calls");
Check(Costs.Estimate(null, 100, 1m, 4m) is null, "Unknown usage must stay unknown");
Check(Math.Abs(Statistics.Percentile([1, 2, 3, 4, 5], .95) - 4.8) < .0001, "Interpolated percentile");
var incident = Tasks.Incidents[0];
Check(Tasks.CheckSummary(incident.DemoAnswer, incident).Passed, "Required supplied facts");
Check(!Tasks.CheckSummary("Payroll batch delayed. Recovery in 15 minutes.", incident).Passed, "Invented recovery time");
Check(!Tasks.CheckSummary("Unknown recovery time.", incident).Passed, "Missing service and impact");
Check(!Tasks.CheckSummary("Billing invoice generation is unavailable. Recovery is soon.", Tasks.Incidents[1]).Passed,
    "Unavailable service must not imply an unknown recovery time");
Check(Tasks.CheckLabel("service request", "service request").Passed, "Exact classifier label");
Check(!Tasks.CheckLabel("incident", "question").Passed, "Wrong classifier label");
ModelCandidate[] models = [new("cloud", "azure", false, true, 128000), new("local", "foundry-local", true, false, 8000)];
Check(Selection.Eligible(models, true, false, 4000).Single().Name == "local", "Offline hard requirement");
Check(Selection.Eligible(models, true, true, 4000).Length == 0, "No valid candidate must remain no match");
Check(CourseContext.ValidateEndpoint("foundry-local", "http://127.0.0.1:39839/v1").AbsolutePath == "/v1/", "Local URL normalization");
foreach (var (provider, url) in new[] {
    ("foundry-local", "https://example.com/v1/"),
    ("foundry-local", "http://localhost:39839/"),
    ("azure", "https://resource.example/api/projects/project"),
    ("foundry", "https://resource.example/api/projects/project"),
    ("foundry-local", "http://user:password@localhost:39839/v1/"),
    ("foundry-local", "http://localhost:39839/v1/?token=ignored") })
{
    bool rejected = false;
    try { CourseContext.ValidateEndpoint(provider, url); } catch (ConfigurationException) { rejected = true; }
    Check(rejected, "Invalid endpoint accepted");
}
string temp = Path.GetTempFileName();
string key = "UNIT02_TEST_" + Guid.NewGuid().ToString("N");
try
{
    File.WriteAllText(temp, $"# comment\n{key}=\"from file\"\n");
    DotEnv.Load(temp);
    Check(Environment.GetEnvironmentVariable(key) == "from file", "Root dotenv loading");
    Environment.SetEnvironmentVariable(key, "process override");
    DotEnv.Load(temp);
    Check(Environment.GetEnvironmentVariable(key) == "process override", "Process overrides file");
}
finally { File.Delete(temp); Environment.SetEnvironmentVariable(key, null); }
Console.WriteLine("All independent cost, selection, quality, endpoint and configuration checks passed.");
