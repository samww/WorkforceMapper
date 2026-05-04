using Microsoft.AspNetCore.Mvc;
using WorkforceMapper.Server.Models;
using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Server.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController(AgentService agent) : ControllerBase
{
    [HttpPost]
    public async Task Post([FromBody] ChatRequest req, CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await agent.RunAsync(
            req.Query,
            req.Month ?? Constants.DefaultMonth,
            async token =>
            {
                await Response.WriteAsync($"data: {token}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            },
            ct);

        await Response.WriteAsync("data: [DONE]\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
