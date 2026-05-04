using Microsoft.AspNetCore.Mvc;
using WorkforceMapper.Server.Models;
using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Server.Controllers;

[ApiController]
[Route("api/mappings")]
public class MappingController(DataService dataService) : ControllerBase
{
    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertMappingRequest request)
    {
        await dataService.UpsertMappingAsync(
            request.resource_id, request.activity_id, request.month, request.percent);
        return Ok();
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(
        [FromQuery] int resource_id,
        [FromQuery] int activity_id,
        [FromQuery] string month)
    {
        var deleted = await dataService.DeleteMappingAsync(resource_id, activity_id, month);
        return deleted ? NoContent() : NotFound();
    }
}
