using Microsoft.AspNetCore.Mvc;
using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Server.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(DataService dataService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string month = "2026-05")
    {
        var projects = await dataService.GetProjectsAsync(month);
        return Ok(projects);
    }
}
