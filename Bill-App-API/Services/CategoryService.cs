using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Interfaces;
using Bill_App_API.Models;
using Bill_App_API.Utils;
using Microsoft.EntityFrameworkCore;
namespace Bill_App_API.Services;

public class CategoryService(BillDbContext dbContext) : ICategoryService
{
  public async Task<bool> AddCategory(CategoryAddRequest req, Guid userId)
  {
    var categories = await GetCategory(userId);
    var originalCategory = categories.FirstOrDefault(item => item.Name == req.Name);
    if (originalCategory is null)
    {
      var category = new Category
      {
        Name = req.Name,
        Type = req.Type,
        UserId = userId
      };
      await dbContext.Categories.AddAsync(category);
      await dbContext.SaveChangesAsync();
      return true;
    }
    return false;
  }
  public async Task<List<Category>> GetCategory(Guid userId)
  {
    var categories = await dbContext.Categories.Where(item => item.UserId == userId).ToListAsync();
    return categories;
  }
}

