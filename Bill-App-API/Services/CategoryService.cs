using Bill_App.Contexts;
using Bill_App.Dtos;
using Bill_App.Interfaces;
using Bill_App.Models;
using Bill_App.Utils;
using Microsoft.EntityFrameworkCore;
namespace Bill_App.Services;

public class CategoryService : ICategoryService
{
  private readonly BillDbContext _dbContext;
  public CategoryService(BillDbContext dbContext)
  {
    _dbContext = dbContext;
  }
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
      await _dbContext.Categories.AddAsync(category);
      await _dbContext.SaveChangesAsync();
      return true;
    }
    return false;
  }
  public async Task<List<Category>> GetCategory(Guid userId)
  {
    var categories = await _dbContext.Categories.Where(item => item.UserId == userId).ToListAsync();
    return categories;
  }
}

