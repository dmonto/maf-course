using System.Net;
using System.Text.Json;
int checks = 0;
void Require(bool pass, string message) { Output.Require(pass, message); checks++; }
async Task Reject(Func<Task> action, string message)
{
    bool rejected = false;
    try { await action(); } catch { rejected = true; }
    Require(rejected, message);
}
using var c = new LabContext(["--demo"]);
await c.InitializeAsync();
Require(c.Documents.Length == 4 && c.Documents.Sum(d => d.Chunks.Length) == 5, "Fixture shape changed.");
var employee = await c.SearchAsync("employee", "hybrid", "Payroll batch retry");
Require(!employee.Any(h => h.DocumentId is "payroll-guide" or "vpn-tenant-b"), "Employee scope leaked restricted evidence.");
Require((await c.SearchAsync("payroll", "hybrid", "Payroll batch retry")).Any(h => h.DocumentId == "payroll-guide"), "Permitted payroll evidence missing.");
Require((await c.SearchAsync("employee", "keyword", "BRAVO-CANARY")).Length == 0, "Tenant A saw tenant B marker.");
Require((await c.SearchAsync("other-tenant", "keyword", "BRAVO-CANARY")).Any(h => h.DocumentId == "vpn-tenant-b"), "Tenant B marker missing.");
int calls = c.Demo!.SearchRequests.Count;
Require((await c.SearchAsync("none", "hybrid", "VPN")).Length == 0 && c.Demo.SearchRequests.Count == calls, "Empty scope invoked Search.");
await c.SearchAsync("employee", "hybrid", "VPN");
var hybrid = c.Demo.SearchRequests.Last();
Require(hybrid.GetProperty("vectorFilterMode").GetString() == "preFilter", "Hybrid preFilter missing.");
Require(hybrid.GetProperty("vectorQueries")[0].GetProperty("k").GetInt32() == 10 && hybrid.GetProperty("top").GetInt32() == 3, "Retrieval budget changed.");
await c.SearchAsync("employee", "keyword", "VPN");
Require(!c.Demo.SearchRequests.Last().TryGetProperty("vectorQueries", out _), "Keyword mode generated vector candidates.");
await c.SearchAsync("employee", "vector", "VPN");
Require(!c.Demo.SearchRequests.Last().TryGetProperty("search", out _), "Vector mode sent a lexical query.");
Require(new CallerScope("tenant's", ["staff's"]).Filter("v'1").Contains("'tenant''s'"), "OData quoting failed.");
c.Demo.FailNextRequest = HttpStatusCode.Forbidden;
await Reject(() => c.SearchAsync("employee", "keyword", "VPN"), "403 became successful missing evidence.");
c.Demo.FailNextIndexItem = true;
await Reject(() => c.Store().SeedAsync(c.Documents, c.Token), "Per-item indexing failure was ignored.");
await c.Store().SeedAsync(c.Documents, c.Token);
await Reject(() => c.Store().SeedAsync([c.Documents[0], c.Documents[0]], c.Token), "Duplicate documents accepted.");
using var client = new HttpClient(new DemoSearchService());
var wrongShape = new SearchStore(c.Settings, new DemoCredential(), client, (_, _) => Task.FromResult(new float[] { 1 }));
await Reject(() => wrongShape.SearchAsync(CallerScope.ForProfile("employee"), "hybrid", "VPN", c.Token), "Wrong vector dimensions accepted.");
await Reject(() => c.SearchAsync("employee", "invalid", "VPN"), "Invalid mode accepted.");
await Reject(() => c.SearchAsync("employee", "hybrid", ""), "Empty query accepted.");
var answer = JsonSerializer.SerializeToElement(await c.AnswerAsync("none", "VPN"), Output.Json);
Require(!answer.GetProperty("modelInvoked").GetBoolean() && answer.GetProperty("evidence").GetArrayLength() == 0, "No-evidence answer crossed inference boundary.");
Output.Result(new { checks, pass = true, executionMode = "local-demo", azureCallsMade = false });
return 0;
