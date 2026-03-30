using Microsoft.AspNetCore.Mvc;
using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Interfaces;

namespace Bill_App_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoryController(ILogger<CategoryController> logger, BillDbContext dbContext, ICategoryService categoryService) : ControllerBase
{
  /// <summary>
  /// 新增類別
  /// </summary>
  /// <param name="req"></param>
  /// <returns></returns>
  [HttpPost]
  public async Task<ActionResult> AddCategory([FromBody] CategoryAddRequest req)
  {
    Guid userId = Guid.Parse("c5824692-cba4-46a2-afc5-05c8a6604abd");
    var isOk = await categoryService.AddCategory(req, userId);
    if (isOk)
    {
      return Ok("新增成功");
    }
    return BadRequest("新增失敗");
  }
}
