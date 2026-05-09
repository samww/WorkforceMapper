using Microsoft.AspNetCore.Mvc;
using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Server.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(DataService dataService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string month = "2026-05")
    {
        var products = await dataService.GetProductRiskAsync(month);
        return Ok(products);
    }
}
