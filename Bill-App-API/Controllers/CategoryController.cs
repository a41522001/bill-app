using Microsoft.AspNetCore.Mvc;
using Bill_App.Contexts;
using Bill_App.Dtos;
using Bill_App.Interfaces;

namespace Bill_App.Controllers;

[ApiController]
[Route("[controller]")]
public class CategoryController : ControllerBase
{
  private readonly BillDbContext _dbContext;
  private readonly ICategoryService _categoryService;
  private readonly ILogger<CategoryController> _logger;
  public CategoryController(ILogger<CategoryController> logger, BillDbContext dbContext, ICategoryService categoryService)
  {
    _logger = logger;
    _dbContext = dbContext;
    _categoryService = categoryService;
  }

  /// <summary>
  /// 新增類別
  /// </summary>
  /// <param name="req"></param>
  /// <returns></returns>
  [HttpPost]
  public async Task<ActionResult> AddCategory([FromBody] CategoryAddRequest req)
  {
    Guid userId = Guid.Parse("c5824692-cba4-46a2-afc5-05c8a6604abd");
    var isOk = await _categoryService.AddCategory(req, userId);
    if (isOk)
    {
      return Ok("新增成功");
    }
    return BadRequest("新增失敗");
  }
}
