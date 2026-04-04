using Bill_App_API.Dtos;
using Bill_App_API.Extensions;
using Bill_App_API.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace Bill_App_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TransactionController(ITransactionService transactionService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> AddTransaction([FromBody] TransactionAddRequest req)
    {
        var userId = HttpContext.GetUserId();
        await transactionService.AddTransaction(req, userId);
        return Ok("新增成功");
    }
    [HttpGet]
    public async Task<ActionResult> GetTransaction([FromQuery] TransactionQueryRequest req)
    {
        var userId = HttpContext.GetUserId();
        var transactions = await transactionService.GetTransaction(req, userId);
        return Ok(transactions);
    }
    [HttpGet("typeList")]
    public ActionResult GetTransactionTypeList()
    {
        var data = transactionService.GetTransactionTypeList();
        return Ok(data);
    }
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTransaction(Guid id)
    {
        var userId = HttpContext.GetUserId();
        await transactionService.DeleteTransaction(id, userId);
        return Ok("刪除成功");
    }
    [HttpPut]
    public async Task<ActionResult> UpdateTransaction([FromBody] TransactionUpdateRequest req)
    {
        var userId = HttpContext.GetUserId();
        await transactionService.UpdateTransaction(req, userId);
        return Ok("修改成功");
    }
}
