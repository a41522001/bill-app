using Bill_App.Contexts;
using Bill_App.Dtos;
using Bill_App.Interfaces;
using Bill_App.Models;
using Bill_App.Utils;
using Microsoft.EntityFrameworkCore;
namespace Bill_App.Services;

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

