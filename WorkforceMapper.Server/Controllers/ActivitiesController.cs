using Microsoft.AspNetCore.Mvc;
using WorkforceMapper.Server.Models;
using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Server.Controllers;

[ApiController]
[Route("api/activities")]
public class ActivitiesController(DataService dataService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? division, [FromQuery] string? activity_type)
    {
        var activities = await dataService.GetActivitiesAsync(division, activity_type);
        return Ok(activities.Select(a => new ActivityDto(
            a.Id, a.Name, a.ActivityType, a.Division,
            a.Application?.Name ?? a.Project?.Name ?? ""
        )));
    }
}
