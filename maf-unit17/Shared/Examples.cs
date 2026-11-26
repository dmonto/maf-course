using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Unit17;
public static class Examples
{
    public static async Task<int> RunAsync(string example,string[] raw)
    {
        Lab.LoadEnv(); var a = new Args(raw);
        Lab.Log("COMMAND", $"{example} {string.Join(' ',raw)}");
        try
        {
            switch (example)
            {
                case "v01": await Copilot(a); break;
                case "v02": EnterpriseCases.Document(a); break;
                case "v03": await EnterpriseCases.Operations(a); break;
                case "v04": EnterpriseCases.Coordination(); break;
                case "v05": if(a.Has("--command")) await Persistent(a); else await RestartDemo(a); break;
                case "v06": await EnterpriseCases.CrmErp(); break;
                case "v07": await TechnicalSupport(a); break;
                case "v08": Scorecard(); break;
                case "v09": Value(a); break;
                case "v10": Pattern(a); Rollout(a); break;
                default: throw new PolicyException("unknown_example");
            }
            return 0;
        }
        catch(PolicyException e){Lab.Log("BLOCKED",e.Message);return 2;}
        catch(Exception e){Lab.Log("ERROR",e.GetType().Name+ (e is HttpRequestException ? ": HTTP dependency failed" : ": execution failed"));return 1;}
    }
    private static string? BaseUrl(Args a) => a.Optional("--base") ?? (a.Has("--azure") ? Lab.Env("UNIT17_AZURE_BASE","http://127.0.0.1:8082") : null);
    private static async Task Copilot(Args a)
    {
        var evidence = await EvidenceSource.ReadAsync(a.Value("--service","vpn"),BaseUrl(a),a.Has("--azure"));
        bool waiting = evidence.Status == "degraded";
        Lab.Log("MILESTONE", $"Evidence: {evidence.Service}/{evidence.Status}; mode={evidence.Mode}");
        Lab.Log("CONCLUSION",waiting?"Review a proposal; no ticket has been created.":"Healthy observation requires no incident.");
        Lab.Emit(new {evidence,state=waiting?"proposal_suggested":"no_action",nextAction=waiting?"Review the proposal.":"Display the observation.",ticket=(string?)null});
    }
    private static async Task TechnicalSupport(Args a)
    {
        string dir=Path.Combine(Path.GetTempPath(),"maf17-"+Guid.NewGuid().ToString("N"));
        try
        {
            using var db=new Store(Path.Combine(dir,"demo.db"));
            var p=await db.StartAsync("vpn-001","vpn",900,BaseUrl(a),a.Has("--azure"));
            Lab.Block("Execution before approval",()=>db.Execute("vpn-001"));
            var review=db.Review("vpn-001","lab-reviewer","approve");
            var result=db.Execute("vpn-001");
            var replay=db.Execute("vpn-001");
            Lab.Check("Replay returns identical ticket",replay.TicketId==result.TicketId);
            Lab.Block("Reusing case ID for email",()=>db.StartAsync("vpn-001","email",900).GetAwaiter().GetResult());
            Lab.Check("Exactly one committed ticket",db.CountTickets()==1);
            Lab.Log("CONCLUSION","Approval authorizes one simulated ticket, not a VPN repair or notification.");
            Lab.Emit(new {proposal=p.State,review=review.State,complete=result.State=="completed",proof=result.TicketId,ticket_count=db.CountTickets(),repair_performed=false});
        }
        finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
    private static void Value(Args a)
    {
        double eligible=a.Number("--eligible",300),manual=a.Number("--manual",6),review=a.Number("--review",2),rate=a.Number("--exception-rate",0.2),investigation=a.Number("--investigation",5);
        if(new[]{eligible,manual,review,rate,investigation}.Any(x=>!double.IsFinite(x)||x<0)||rate>1)throw new PolicyException("invalid_assumptions");
        double baseline=eligible*manual,reviews=eligible*review,extra=eligible*rate*investigation,net=baseline-reviews-extra;
        Lab.Log("MILESTONE",$"Manual={baseline}; review={reviews}; investigation={extra}; net={net} minutes");
        Lab.Log("CONCLUSION","Potential active time saved; maintenance and operating costs excluded.");
        Lab.Emit(new {eligible,manual_minutes=baseline,review_minutes=reviews,investigation_minutes=extra,net_minutes_before_operating_cost=net});
    }
    private static void Pattern(Args a)
    {
        var patterns=new Dictionary<string,(string Pattern,string Boundary)>{["independent-reads"]=("bounded_concurrency","deadline and partial-result policy"),["single-review"]=("state_machine","stored proposal and state transitions"),["specialists"]=("sequential","typed outputs"),["handoff"]=("controlled_handoff","identity and scope"),["long-process"]=("durable_orchestration","effect tracking and recovery")};
        string scenario=a.Value("--scenario","independent-reads");if(!patterns.TryGetValue(scenario,out var p))throw new PolicyException("unsupported_scenario");
        Lab.Log("MILESTONE",$"{scenario}: {p.Pattern}; {p.Boundary}");Lab.Log("CONCLUSION","Architecture selection only; no concurrent agents executed.");Lab.Emit(new {scenario,pattern=p.Pattern,required_boundary=p.Boundary});
    }
    private static async Task Persistent(Args a)
    {
        string cmd=a.Value("--command","inspect"), id=a.Value("--case-id","vpn-001"),path=a.Value("--db",Lab.Env("UNIT17_DB","state/unit17-dotnet.db"));
        using var db=new Store(path);
        string? url=a.Optional("--base");bool azure=a.Has("--azure")||a.Has("--require-azure");if(azure&&url is null)url=Lab.Env("UNIT17_AZURE_BASE","http://127.0.0.1:8082");
        CaseRecord result=cmd switch
        {
            "start"=>await db.StartAsync(id,a.Value("--service","vpn"),a.Int("--ttl",int.Parse(Lab.Env("UNIT17_TTL","900"),CultureInfo.InvariantCulture)),url,azure),
            "inspect"=>db.Inspect(id),
            "review"=>db.Review(id,a.Value("--reviewer","lab-reviewer"),a.Value("--decision","approve")),
            "execute"=>db.Execute(id),_=>throw new PolicyException("unknown_command")
        };
        Lab.Log("CONCLUSION",$"Committed case state: {result.State}; ticket={result.TicketId??"none"}");Lab.Emit(result);
    }
    private static async Task RestartDemo(Args a)
    {
        string dir=Path.Combine(Path.GetTempPath(),"maf17-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string db=Path.Combine(dir,"demo.db");
        async Task Step(int expected,params string[] options)
        {
            string dll=Assembly.GetEntryAssembly()!.Location;
            var info=new ProcessStartInfo("dotnet"){UseShellExecute=false};info.ArgumentList.Add(dll);info.ArgumentList.Add("--db");info.ArgumentList.Add(db);
            foreach(var option in options)info.ArgumentList.Add(option);
            Lab.Log("COMMAND",$"dotnet \"{dll}\" --db \"{db}\" {string.Join(' ',options)}");
            using var p=Process.Start(info)??throw new InvalidOperationException("Cannot launch child");await p.WaitForExitAsync();Lab.Check($"Child exit code {expected}",p.ExitCode==expected);
        }
        try
        {
            var start=new List<string>{"--command","start","--case-id","vpn-001","--ttl","900"};if(a.Has("--azure"))start.Add("--azure");if(a.Optional("--base") is { } url){start.Add("--base");start.Add(url);}
            await Step(0,start.ToArray());await Step(0,"--command","inspect");await Step(2,"--command","execute");await Step(0,"--command","review");await Step(0,"--command","execute");await Step(0,"--command","execute");
            await Step(0,"--command","start","--case-id","mail","--service","email");await Step(2,"--command","execute","--case-id","mail");
            await Step(0,"--command","start","--case-id","declined");await Step(0,"--command","review","--case-id","declined","--decision","reject");await Step(2,"--command","execute","--case-id","declined");
            await Step(0,"--command","start","--case-id","late","--ttl","0");await Step(2,"--command","review","--case-id","late");await Step(2,"--command","execute","--case-id","late");
            using var store=new Store(db);var result=store.Inspect("vpn-001");Lab.Check("Exactly one ticket across processes",store.CountTickets()==1&&result.TicketId=="SIM-vpn-001");
            Lab.Log("CONCLUSION","Restart, approval, replay, no-action, rejection and expiry verified; all effects simulated.");Lab.Emit(new {result.State,result.TicketId,ticket_count=store.CountTickets()});
        }
        finally {Directory.Delete(dir,true);}
    }
    private static void Scorecard()
    {
        var rows=File.ReadLines(Path.Combine(AppContext.BaseDirectory,"pilot.csv")).Skip(1).Where(line=>!string.IsNullOrWhiteSpace(line)).Select(line=>
        {
            var fields=line.Split(',');
            return (Service:fields[0],State:fields[1],Correct:fields[2]=="1",Cost:decimal.Parse(fields[3],CultureInfo.InvariantCulture));
        }).ToArray();
        void Report(string name,IEnumerable<(string Service,string State,bool Correct,decimal Cost)> values)
        {
            var group=values.ToArray();int correct=group.Count(r=>r.State=="completed"&&r.Correct);decimal cost=group.Sum(r=>r.Cost);decimal? costPer=correct==0?null:cost/correct;
            Lab.Log("MILESTONE",$"{name}: eligible={group.Length}; correct={correct}; total cost={cost}; cost per correct={costPer?.ToString()??"n/a"}");
            Lab.Emit(new {group=name,eligible=group.Length,correct,completion_rate=(decimal)correct/group.Length,cost_eur_per_correct_ticket=costPer});
        }
        Report("all",rows);foreach(var g in rows.GroupBy(r=>r.Item1))Report(g.Key,g);Lab.Log("CONCLUSION","Synthetic scorecard, not measured production savings.");
    }
    private static void Rollout(Args a)
    {
        string stage=a.Value("--stage","pilot"),cohort=a.Value("--cohort","pilot");if(stage is not("assist" or "pilot" or "expand"))throw new PolicyException("unknown_stage");
        bool paused=a.Has("--stop-writes"),allowed=!paused&&(stage=="expand"||(stage=="pilot"&&cohort=="pilot"));
        Lab.Log("MILESTONE",$"Stage={stage}; cohort={cohort}; new execution allowed={allowed}; existing case inspection=true");
        Lab.Log("CONCLUSION","Rollout permission is separate from case approval; this example does not modify the database.");Lab.Emit(new {stage,cohort,inspect_existing=true,may_propose=stage!="assist",may_execute_after_case_approval=allowed,new_execution_paused=paused});
    }
}
