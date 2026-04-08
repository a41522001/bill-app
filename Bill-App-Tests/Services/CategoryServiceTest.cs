using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Enums;
using Bill_App_API.Exceptions;
using Bill_App_API.Models;
using Bill_App_API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Text;
namespace Bill_App_Tests.Services;

public class CategoryServiceTest
{
    private static BillDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new BillDbContext(options);
    }
    [Fact]
    public async Task AddCategory_Return_Correct_Value()
    {
        var dbContext = CreateDbContext();
        var categoryService = new CategoryService(dbContext);
        var categoryName = "飲食";
        var req = new CategoryAddRequest(categoryName, TransactionTypeEnum.Expense);
        var userId = Guid.NewGuid();
        await categoryService.AddCategory(req, userId);
        var cartgory = await dbContext.Categories.FirstOrDefaultAsync(c => c.UserId == userId);
        Assert.NotNull(cartgory);
        Assert.Equal(categoryName, cartgory.Name);
    }
    [Fact]
    public async Task AddCategory_Throw_Exception_Repeat_Category()
    {
        var dbContext = CreateDbContext();
        var categoryService = new CategoryService(dbContext);
        var categoryName = "飲食";
        var req = new CategoryAddRequest(categoryName, TransactionTypeEnum.Expense);
        var userId = Guid.NewGuid();
        await categoryService.AddCategory(req, userId);
        await Assert.ThrowsAsync<ApiException>(async () => await categoryService.AddCategory(req, userId));
    }
    [Fact]
    public async Task GetCategory_Return_Correct_Value()
    {
        var dbContext = CreateDbContext();
        var categoryService = new CategoryService(dbContext);
        var userId = Guid.NewGuid();
        var categoryName1 = "飲食";
        var categoryName2 = "交通";
        var req1 = new CategoryAddRequest(categoryName1, TransactionTypeEnum.Expense);
        var req2 = new CategoryAddRequest(categoryName2, TransactionTypeEnum.Expense);
        await categoryService.AddCategory(req1, userId);
        await categoryService.AddCategory(req2, userId);
        var actual = await categoryService.GetCategory(userId);
        Assert.Equal(categoryName1, actual[0].Name);
        Assert.Equal(categoryName2, actual[1].Name);
        Assert.Equal(2, actual.Count);
    }
    [Fact]
    public async Task DeleteCategory_Test()
    {
        var dbContext = CreateDbContext();
        var categoryService = new CategoryService(dbContext);
        var userId = Guid.NewGuid();
        var categoryName = "飲食";
        var req1= new CategoryAddRequest(categoryName, TransactionTypeEnum.Expense);
        await categoryService.AddCategory(req1, userId);
        var originalCategories = await categoryService.GetCategory(userId);
        await categoryService.DeleteCategory(userId, originalCategories[0].Id);
        var newCategories = await categoryService.GetCategory(userId);
        Assert.Equal(0, newCategories.Count);
    }
}
