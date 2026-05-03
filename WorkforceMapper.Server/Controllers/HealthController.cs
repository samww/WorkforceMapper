using Microsoft.AspNetCore.Mvc;
using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Server.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(DataService dataService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var (resourceCount, mappingCount) = await dataService.GetHealthAsync();
        return Ok(new { status = "ok", resource_count = resourceCount, mapping_count = mappingCount });
    }
}
