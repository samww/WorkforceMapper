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
        var resource = await dataService.GetResourceByPkAsync(request.resource_id);
        if (resource is null) return NotFound();

        var currentMappings = await dataService.GetMappingsForResourceMonthAsync(
            request.resource_id, request.month);

        var validation = AllocationValidator.ValidateUpsert(
            resource, request.month, currentMappings, request.activity_id, request.percent);

        if (!validation.IsValid)
            return BadRequest(new { error = ToErrorCode(validation.Reason) });

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
        var resource = await dataService.GetResourceByPkAsync(resource_id);
        if (resource is null) return NotFound();

        var validation = AllocationValidator.ValidateDelete(resource, month);
        if (!validation.IsValid)
            return BadRequest(new { error = ToErrorCode(validation.Reason) });

        var deleted = await dataService.DeleteMappingAsync(resource_id, activity_id, month);
        return deleted ? NoContent() : NotFound();
    }

    private static string ToErrorCode(RejectionReason reason) => reason switch
    {
        RejectionReason.WouldExceedCap => "would_exceed_cap",
        RejectionReason.ResourceInactiveInMonth => "resource_inactive_in_month",
        _ => "unknown"
    };
}
