using Microsoft.AspNetCore.Mvc;
using Bill_App_API.Dtos;
using Bill_App_API.Interfaces;
using Bill_App_API.Extensions;
namespace Bill_App_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoryController(ICategoryService categoryService) : ControllerBase
{
    /// <summary>
    /// 新增類別
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult> AddCategory([FromBody] CategoryAddRequest req)
    {
        var userId = HttpContext.GetUserId();
        await categoryService.AddCategory(req, userId);
        return Ok("新增成功");
    }
    /// <summary>
    /// 取得類別
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public async Task<ActionResult> GetCategories()
    {
        var userId = HttpContext.GetUserId();
        var categories = await categoryService.GetCategory(userId);
        return Ok(categories);
    }
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteCategory(Guid id)
    {
        var userId = HttpContext.GetUserId();
        await categoryService.DeleteCategory(userId, id);
        return Ok("刪除成功");
    }
}
