using Microsoft.AspNetCore.Http;
using Microsoft.Agents.AI;
namespace Unit04.Common;
public interface IChatHandler
{
    Task<IResult> RunAsync(ChatRequest request, AIAgent agent, HttpContext context);
}
