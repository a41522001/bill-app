using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Interfaces;
using Bill_App_API.Models;
using Bill_App_API.Exceptions;
using Microsoft.EntityFrameworkCore;
namespace Bill_App_API.Services;

public class TransactionService(BillDbContext dbContext) : ITransactionService
{
    public async Task AddTransaction(TransactionAddRequest req, Guid userId)
    {
        var category = await dbContext.Categories.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == req.CategoryId);
        if (category is null)
        {
            throw new ApiException("無此類別", 400);
        }
        var transaction = new Transaction
        {
            Type = category.Type,
            CategoryId = req.CategoryId,
            Amount = req.Amount,
            Note = req.Note,
            UserId =  userId
        };
        await dbContext.Transactions.AddAsync(transaction);
        await dbContext.SaveChangesAsync();
    }
}
