using Microsoft.AspNetCore.Mvc;
using WorkforceMapper.Server.Models;
using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Server.Controllers;

[ApiController]
[Route("api/resources")]
public class ResourcesController(DataService dataService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? division, [FromQuery] string? month)
    {
        var m = month ?? Constants.DefaultMonth;
        var resources = await dataService.GetResourcesAsync(division, null);
        return Ok(resources.Select(r => new ResourceDto(
            r.ResourceId, r.Name, r.ResourceType, r.Company, r.Seniority, r.RoleType,
            r.Division, r.Country, r.MonthlyRate,
            IsActive(r, m),
            r.Mappings
                .Where(mp => mp.Month == m)
                .Select(mp => new MappingDto(mp.ActivityId, mp.Activity.Name, mp.Percent, mp.Month))
                .ToList()
        )));
    }

    private static bool IsActive(Resource r, string month) => month[5..] switch
    {
        "01" => r.Jan == 1, "02" => r.Feb == 1, "03" => r.Mar == 1,
        "04" => r.Apr == 1, "05" => r.May == 1, "06" => r.Jun == 1,
        "07" => r.Jul == 1, "08" => r.Aug == 1, "09" => r.Sep == 1,
        "10" => r.Oct == 1, "11" => r.Nov == 1, "12" => r.Dec == 1,
        _ => false
    };
}
