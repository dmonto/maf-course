using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Unit17;
public static class EnterpriseCases
{
    private sealed record Invoice(string Supplier,string Number,decimal Net,decimal Tax,decimal Total,string Currency);
    public static void Document(Args a)
    {
        // A recorded candidate from document extraction; no model inference in this example.
        string raw = a.Has("--invalid") ? """{"supplier":"SUP-01","number":"INV-42","net":100,"tax":21,"total":999,"currency":"EUR"}""" : """{"supplier":"SUP-01","number":"INV-42","net":100,"tax":21,"total":121,"currency":"EUR"}""";
        Lab.Log("FIXTURE",$"Recorded structured extraction: {raw}");
        var invoice=JsonSerializer.Deserialize<Invoice>(raw,Lab.Json) ?? throw new PolicyException("missing_document");
        bool accepted=invoice.Supplier=="SUP-01" && !string.IsNullOrWhiteSpace(invoice.Number) && invoice.Currency=="EUR" && invoice.Net>=0 && invoice.Tax>=0 && invoice.Net+invoice.Tax==invoice.Total;
        Lab.Check("Validation matches the expected case",accepted!=a.Has("--invalid"));
        Lab.Log("MILESTONE",accepted?"Validated draft ready for review":"Exception queue: invalid totals or fields");
        Lab.Log("CONCLUSION","Extraction is a candidate, not an authorized accounting entry.");
        Lab.Emit(new {accepted,invoice,posted=false});
    }
    public static async Task Operations(Args a)
    {
        string? url=a.Optional("--base") ?? (a.Has("--azure")?Lab.Env("UNIT17_AZURE_BASE","http://127.0.0.1:8082"):null);
        var observations=await Task.WhenAll(new[]{"vpn","email"}.Select(service=>EvidenceSource.ReadAsync(service,url,a.Has("--azure"))));
        var degraded=observations.Where(e=>e.Status=="degraded").Select(e=>e.Service).ToArray();
        Lab.Check("Two distinct service observations",observations.Select(e=>e.Service).Distinct().Count()==2);
        Lab.Log("MILESTONE",$"Degraded services: {string.Join(",",degraded)}");
        Lab.Log("CONCLUSION","Operations assistant prioritizes a review; no remediation is executed.");
        Lab.Emit(new {observations,degraded,action="review",repair_performed=false});
    }
    public static void Coordination()
    {
        // Simulated trusted roles; actual identity and durable approval storage belong to production.
        var approvals=new Dictionary<string,string>();
        void Approve(string role,string actor,string version)
        {
            if(role is not("operations" or "service-owner") || version!="evidence-1" || approvals.ContainsKey(role) || approvals.ContainsValue(actor))throw new PolicyException("invalid_approval");
            approvals.Add(role,actor);Lab.Log("MILESTONE",$"{role} approved evidence-1 as {actor}");
        }
        bool Ready()=>approvals.ContainsKey("operations") && approvals.ContainsKey("service-owner");
        Approve("operations","ops-01","evidence-1");
        Lab.Check("Single approval cannot release handoff",!Ready());
        Lab.Block("Same actor cannot approve both roles",()=>Approve("service-owner","ops-01","evidence-1"));
        Lab.Block("Stale evidence cannot be approved",()=>Approve("service-owner","owner-01","old-evidence"));
        Approve("service-owner","owner-01","evidence-1");
        Lab.Check("Both roles approved the same evidence",Ready());
        Lab.Log("CONCLUSION","Cross-department handoff ready; no production action executed.");
        Lab.Emit(new {ready=Ready(),approvals,evidenceVersion="evidence-1"});
    }
    private sealed record Customer(string Id,string Status);
    private sealed record Draft(string CustomerId,string OperationId,string Description);
    private sealed record Receipt(string Id,Draft Draft);
    public static async Task CrmErp()
    {
        // Actual loopback HTTP; endpoints implement synthetic CRM/ERP contracts only.
        var receipts=new ConcurrentDictionary<string,Receipt>();
        var builder=WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app=builder.Build();
        app.MapGet("/crm/customers/{id}",(string id)=>id=="C-001"?Results.Ok(new Customer(id,"active")):Results.NotFound());
        app.MapPost("/erp/drafts",(Draft draft)=>
        {
            if(draft.CustomerId!="C-001" || draft.OperationId!="OP-001" || string.IsNullOrWhiteSpace(draft.Description))return Results.BadRequest();
            var receipt=receipts.GetOrAdd(draft.OperationId,_=>new Receipt("DRAFT-001",draft));
            return receipt.Draft==draft?Results.Ok(receipt):Results.Conflict();
        });
        await app.StartAsync();
        try
        {
            string address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client=new HttpClient{BaseAddress=new Uri(address),Timeout=TimeSpan.FromSeconds(10)};
            Lab.Log("HTTP REQUEST",$"GET {address}/crm/customers/C-001");
            using var get=await client.GetAsync("/crm/customers/C-001");Lab.Log("HTTP RESPONSE",$"{(int)get.StatusCode}");get.EnsureSuccessStatusCode();
            var customer=await get.Content.ReadFromJsonAsync<Customer>(Lab.Json);
            Lab.Check("CRM customer active",customer?.Status=="active" && customer.Id=="C-001");
            var draft=new Draft("C-001","OP-001","Synthetic incident follow-up");
            async Task<Receipt> Send(Draft value)
            {
                Lab.Log("HTTP REQUEST",$"POST {address}/erp/drafts {JsonSerializer.Serialize(value,Lab.Json)}");
                using var post=await client.PostAsJsonAsync("/erp/drafts",value,Lab.Json);Lab.Log("HTTP RESPONSE",$"{(int)post.StatusCode}");post.EnsureSuccessStatusCode();
                return await post.Content.ReadFromJsonAsync<Receipt>(Lab.Json)??throw new PolicyException("missing_receipt");
            }
            var first=await Send(draft);var replay=await Send(draft);
            Lab.Check("Same operation returns the same draft",first==replay && receipts.Count==1);
            Lab.Log("CONCLUSION","CRM read and ERP draft verified over HTTP; no corporate systems or real orders affected.");
            Lab.Emit(new {customer,receipt=first,receiptCount=receipts.Count,posted=false});
        }
        finally {await app.StopAsync();}
    }
}
