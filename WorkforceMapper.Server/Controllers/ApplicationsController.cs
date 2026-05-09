using Microsoft.AspNetCore.Mvc;
using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Server.Controllers;

[ApiController]
[Route("api/applications")]
public class ApplicationsController(DataService dataService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string month = "2026-05")
    {
        var apps = await dataService.GetApplicationsAsync(month);
        return Ok(apps);
    }
}
