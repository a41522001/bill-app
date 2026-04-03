using Bill_App_API.Dtos;
using Bill_App_API.Extensions;
using Bill_App_API.Interfaces;
using Bill_App_API.Services;
using Microsoft.AspNetCore.Mvc;
namespace Bill_App_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TransactionController(ITransactionService TransactionService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> AddTransaction([FromBody] TransactionAddRequest req)
    {
        var userId = HttpContext.GetUserId();
        await TransactionService.AddTransaction(req, userId);
        return Ok("新增成功");
    }
}
