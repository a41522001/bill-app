using Bill_App_API.Dtos;
using Bill_App_API.Extensions;
using Bill_App_API.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace Bill_App_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatisticsController(IStatisticsService statisticsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetStatistics([FromQuery] StatisticsQueryRequest req)
    {
        var userId = HttpContext.GetUserId();
        var result = await statisticsService.GetStatistics(req, userId);
        return Ok(result);
    }
}
