using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Enums;
using Bill_App_API.Exceptions;
using Bill_App_API.Interfaces;
using Bill_App_API.Models;
using Microsoft.EntityFrameworkCore;
namespace Bill_App_API.Services;

public class CategoryService(BillDbContext dbContext) : ICategoryService
{
    /// <summary>
    /// 新增類別
    /// </summary>
    /// <param name="req"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public async Task AddCategory(CategoryAddRequest req, Guid userId)
    {
        var originalCategory = await dbContext.Categories.FirstOrDefaultAsync(item => item.Name == req.Name && item.UserId == userId && item.DeletedAt == null);
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
            return;
        }
        throw new ApiException("已有重覆類別");
    }
    /// <summary>
    /// 取得類別
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<List<CategoryResponse>> GetCategory(Guid userId)
    {
        var result = await dbContext.Categories.Where(item => item.UserId == userId && item.DeletedAt == null).ToListAsync();
        var categories = result.Select(item =>
        {
            string typeName = item.Type == TransactionTypeEnum.Income ? "收入" : "支出";
            return new CategoryResponse(
                Id: item.Id,
                Name: item.Name,
                TypeName: typeName,
                Type: item.Type
            );
        }).ToList();
        return categories;
    }
    /// <summary>
    /// 刪除類別
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="categoryId"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public async Task DeleteCategory(Guid userId, Guid categoryId)
    {
        var category = await dbContext.Categories.FirstOrDefaultAsync(item => item.Id == categoryId && item.UserId == userId);
        if (category is null)
        {
            throw new ApiException("無此類別");
        }
        if (category.DeletedAt is not null)
        {
            throw new ApiException("類別已刪除");
        }
        category.DeletedAt = DateTime.UtcNow;
        dbContext.Categories.Update(category);
        await dbContext.SaveChangesAsync();
    }
}

