#pragma warning disable SKEXP0080

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Process;

string outcome = args.FirstOrDefault() ?? "approved";
if (outcome is not ("approved" or "expired" or "budget"))
    throw new ArgumentException("Use approved, expired or budget as the first argument.");
int statusMs = args.Length > 1 ? int.Parse(args[1]) : 1000;
int policyMs = args.Length > 2 ? int.Parse(args[2]) : 1500;
if (statusMs < 1 || policyMs < 1 || statusMs > 10000 || policyMs > 10000)
    throw new ArgumentException("Delays must be between 1 and 10000 milliseconds.");
var request = new SupportRequest("INC-DEMO-001", "vpn", outcome, statusMs, policyMs);

ProcessBuilder builder = new("ConcurrentEvidenceReview");
var status = builder.AddStepFromType<ReadStatusStep>();
var policy = builder.AddStepFromType<ReadPolicyStep>();
var validate = builder.AddStepFromType<ValidateEvidenceStep>();
var review = builder.AddStepFromType<ReviewStep>();
var finish = builder.AddStepFromType<FinishStep>();

// FAN-OUT: the same request activates two independent steps.
builder.OnInputEvent("Start")
    .SendEventTo(new ProcessFunctionTargetBuilder(status, parameterName: "request"))
    .SendEventTo(new ProcessFunctionTargetBuilder(policy, parameterName: "request"));

// FAN-IN: each result fills a different required function parameter.
// ValidateEvidenceStep runs only when BOTH inputs are available.
status.OnFunctionResult()
    .SendEventTo(new ProcessFunctionTargetBuilder(validate, parameterName: "status"));
policy.OnFunctionResult()
    .SendEventTo(new ProcessFunctionTargetBuilder(validate, parameterName: "policy"));

// EvidenceValidated --> ReviewPending.
validate.OnEvent("RequestReview")
    .SendEventTo(new ProcessFunctionTargetBuilder(review, parameterName: "evidence"));

// ReviewPending --> one terminal outcome.
review.OnEvent("TrustedReview")
    .SendEventTo(new ProcessFunctionTargetBuilder(finish, functionName: "Completed", parameterName: "evidence"));
review.OnEvent("ApprovalExpired")
    .SendEventTo(new ProcessFunctionTargetBuilder(finish, functionName: "Expired", parameterName: "evidence"));
review.OnEvent("TurnLimitReached")
    .SendEventTo(new ProcessFunctionTargetBuilder(finish, functionName: "BudgetExhausted", parameterName: "evidence"));
finish.OnEvent("Finished").StopProcess();

KernelProcess workflow = builder.Build();
Kernel kernel = Kernel.CreateBuilder().Build();
TraceLog.Write("BUILD", "ProcessBuilder.Build(): workflow definition created.");
TraceLog.Write("STATE", "EvidencePending");
TraceLog.Write("SK", "StartAsync(kernel, event: Start): activate both evidence branches.");

await using var execution = await workflow.StartAsync(kernel, new KernelProcessEvent
{
    Id = "Start",
    Data = request
});

TraceLog.Verify(outcome);
TraceLog.Write("CONCLUSION", "Both evidence branches overlapped; the join ran once; one terminal outcome was reached.");
