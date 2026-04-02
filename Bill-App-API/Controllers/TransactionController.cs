using Microsoft.AspNetCore.Mvc;
using Bill_App_API.Dtos;
using Bill_App_API.Interfaces;
using Bill_App_API.Extensions;
namespace Bill_App_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TransactionController() : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> AddTransaction([FromBody] TransactionAddRequest req)
    {
        var userId = HttpContext.GetUserId();
        return Ok("新增成功");
    }
}
