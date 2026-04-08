using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Enums;
using Bill_App_API.Exceptions;
using Bill_App_API.Models;
using Bill_App_API.Services;
using Microsoft.EntityFrameworkCore;

namespace Bill_App_Tests.Services;

public class TransactionServiceTest
{
    private static BillDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new BillDbContext(options);
    }
    private static async Task<Category> CreateTestCategory(BillDbContext dbContext, Guid userId)
    {
        var category = new Category
        {
            Name = "飲食",
            Type = TransactionTypeEnum.Expense,
            UserId = userId
        };
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();
        return category;
    }
    [Fact]
    public async Task AddTransaction_Return_Correct()
    {
        var dbContext = CreateDbContext();
        var transactionService = new TransactionService(dbContext);
        var userId = Guid.NewGuid();
        var category = await CreateTestCategory(dbContext, userId);
        var transaction = new TransactionAddRequest(
            CategoryId: category.Id,
            Amount: 100,
            Note: "午餐"
        );
        await transactionService.AddTransaction(transaction, userId);
        var actual = await dbContext.Transactions.Include(item => item.Category).FirstOrDefaultAsync(item => item.UserId == userId);
        Assert.NotNull(actual);
        Assert.Equal(transaction.CategoryId, actual.Category.Id);
        Assert.Equal(transaction.Amount, actual.Amount);
        Assert.Equal(transaction.Note, actual.Note);
        Assert.Equal(TransactionTypeEnum.Expense, actual.Category.Type);
    }
    [Fact]
    public async Task AddTransaction_Throw_Exception_Invalid_Category()
    {
        var dbContext = CreateDbContext();
        var transactionService = new TransactionService(dbContext);
        var userId = Guid.NewGuid();
        var transaction = new TransactionAddRequest(
            CategoryId: Guid.NewGuid(),
            Amount: 100,
            Note: "午餐"
        );
        await Assert.ThrowsAsync<ApiException>(async () => await transactionService.AddTransaction(transaction, userId));
    }
    [Fact]
    public async Task DeleteTransaction_Success()
    {
        var dbContext = CreateDbContext();
        var transactionService = new TransactionService(dbContext);
        var userId = Guid.NewGuid();
        var category = await CreateTestCategory(dbContext, userId);
        var transactionId = Guid.NewGuid();
        dbContext.Transactions.Add(new Transaction
        {
            Id = transactionId,
            UserId = userId,
            CategoryId = category.Id,
            Amount = 100,
            Note = "午餐"
        });
        await dbContext.SaveChangesAsync();
        await transactionService.DeleteTransaction(transactionId, userId);
        var newTransaction = await dbContext.Transactions.FirstOrDefaultAsync(item => item.Id == transactionId);
        Assert.Null(newTransaction);
    }
    [Fact]
    public async Task DeleteTransaction_Throw_Exception_Invalid_Transaction()
    {
        var dbContext = CreateDbContext();
        var transactionService = new TransactionService(dbContext);
        var userId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        await Assert.ThrowsAsync<ApiException>(async () => await transactionService.DeleteTransaction(transactionId, userId));
    }
    [Fact]
    public async Task UpdateTransaction_Success()
    {
        var dbContext = CreateDbContext();
        var transactionService = new TransactionService(dbContext);
        var userId = Guid.NewGuid();
        var category = await CreateTestCategory(dbContext, userId);
        var transactionId = Guid.NewGuid();
        dbContext.Transactions.Add(new Transaction
        {
            Id = transactionId,
            UserId = userId,
            CategoryId = category.Id,
            Amount = 100,
            Note = "午餐"
        });
        await dbContext.SaveChangesAsync();
        var expect = new TransactionUpdateRequest(
            Id: transactionId,
            Note: "晚餐",
            CategoryId: category.Id,
            Amount: 200
        );
        await transactionService.UpdateTransaction(expect, userId);
        var actual = await dbContext.Transactions.Include(c => c.Category).FirstOrDefaultAsync(item => item.Id == transactionId);
        Assert.Equal(expect.Note, actual.Note);
        Assert.Equal(category.Id, actual.Category.Id);
        Assert.Equal(expect.Amount, actual.Amount);
    }
    [Fact]
    public async Task UpdateTransaction_Throw_Not_Transaction()
    {
        var dbContext = CreateDbContext();
        var transactionService = new TransactionService(dbContext);
        var userId = Guid.NewGuid();
        var category = await CreateTestCategory(dbContext, userId);
        var req = new TransactionUpdateRequest(
            Id: Guid.NewGuid(),
            Note: "晚餐",
            CategoryId: category.Id,
            Amount: 200
        );
        var exception = await Assert.ThrowsAsync<ApiException>(async () => await transactionService.UpdateTransaction(req, userId));
        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("無此交易明細", exception.Message);
    }
    [Fact]
    public async Task UpdateTransaction_Throw_Not_Category()
    {
        var dbContext = CreateDbContext();
        var transactionService = new TransactionService(dbContext);
        var userId = Guid.NewGuid();
        var category = await CreateTestCategory(dbContext, userId);
        var transactionId = Guid.NewGuid();
        dbContext.Transactions.Add(new Transaction
        {
            Id = transactionId,
            UserId = userId,
            CategoryId = category.Id,
            Amount = 100,
            Note = "午餐"
        });
        await dbContext.SaveChangesAsync();
        var req = new TransactionUpdateRequest(
            Id: transactionId,
            Note: "晚餐",
            CategoryId: Guid.NewGuid(),
            Amount: 200
        );
        var exception = await Assert.ThrowsAsync<ApiException>(async () => await transactionService.UpdateTransaction(req, userId));
        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("無此類別", exception.Message);
    }
    [Fact]
    public async Task GetTransaction_By_CategoryId()
    {
        var dbContext = CreateDbContext();
        var transactionService = new TransactionService(dbContext);
        var userId = Guid.NewGuid();
        var category = await CreateTestCategory(dbContext, userId);
        var transactionId = Guid.NewGuid();
        var expect = new Transaction
        {
            Id = transactionId,
            UserId = userId,
            CategoryId = category.Id,
            Amount = 100,
            Note = "午餐"
        };
        dbContext.Transactions.Add(expect);
        await dbContext.SaveChangesAsync();
        var query = new TransactionQueryRequest(
            Type: null,
            CategoryId: category.Id,
            EndDate: null,
            StartDate: null
        );
        var actual = await transactionService.GetTransaction(query, userId);
        Assert.Equal(expect.Id, actual.Data[0].Id);
        Assert.Equal(expect.Amount, actual.Data[0].Amount);
        Assert.Equal(expect.Note, actual.Data[0].Note);
        Assert.Equal(expect.CategoryId, actual.Data[0].CategoryId);
        Assert.Equal(1, actual.Meta.Total);
        Assert.Equal(1, actual.Meta.TotalPages);
    }
    [Fact]
    public async Task GetTransaction_By_Type()
    {
        var dbContext = CreateDbContext();
        var transactionService = new TransactionService(dbContext);
        var userId = Guid.NewGuid();
        var category = await CreateTestCategory(dbContext, userId);
        var transactionId = Guid.NewGuid();
        var expect = new Transaction
        {
            Id = transactionId,
            UserId = userId,
            CategoryId = category.Id,
            Amount = 100,
            Note = "午餐"
        };
        dbContext.Transactions.Add(expect);
        await dbContext.SaveChangesAsync();
        var query = new TransactionQueryRequest(
            Type: TransactionTypeEnum.Expense,
            CategoryId: null,
            EndDate: null,
            StartDate: null
        );
        var actual = await transactionService.GetTransaction(query, userId);
        Assert.Equal(expect.Id, actual.Data[0].Id);
        Assert.Equal(expect.Amount, actual.Data[0].Amount);
        Assert.Equal(expect.Note, actual.Data[0].Note);
        Assert.Equal(TransactionTypeEnum.Expense, actual.Data[0].Type);
        Assert.Equal(1, actual.Meta.Total);
        Assert.Equal(1, actual.Meta.TotalPages);
    }
    [Fact]
    public void GetTransactionTypeList()
    {
        var dbContext = CreateDbContext();
        var transactionService = new TransactionService(dbContext);
        var actual = transactionService.GetTransactionTypeList();
        Assert.Equal(2, actual.Count);
    }
}
