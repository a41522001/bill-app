using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Enums;
using Bill_App_API.Exceptions;
using Bill_App_API.Interfaces;
using Bill_App_API.Models;
using Microsoft.EntityFrameworkCore;
namespace Bill_App_API.Services;

public class TransactionService(BillDbContext dbContext) : ITransactionService
{
    /// <summary>
    /// 新增交易明細
    /// </summary>
    /// <param name="req"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public async Task AddTransaction(TransactionAddRequest req, Guid userId)
    {
        var category = await dbContext.Categories.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == req.CategoryId);
        if (category is null)
        {
            throw new ApiException("無此類別", 400);
        }
        var transaction = new Transaction
        {
            CategoryId = req.CategoryId,
            Amount = req.Amount,
            Note = req.Note,
            UserId = userId
        };
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// 取得交易明細
    /// </summary>
    /// <param name="req"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<PaginatedResponse<TransactionResponse>> GetTransaction(TransactionQueryRequest req, Guid userId)
    {
        var query = dbContext.Transactions.Where(item => item.UserId == userId);
        if (req.CategoryId is not null)
        {
            query = query.Where(item => item.CategoryId == req.CategoryId);
        }
        if (req.Type is not null)
        {
            query = query.Where(item => item.Category.Type == req.Type);
        }
        if (req.StartDate is not null)
        {
            query = query.Where(item => item.CreatedAt >= req.StartDate);
        }
        if (req.EndDate is not null)
        {
            query = query.Where(item => item.CreatedAt <= req.EndDate);
        }
        var total = await query.CountAsync();

        var transactions = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((req.Page - 1) * req.Limit)
            .Take(req.Limit)
            .Select(item => new TransactionResponse
            (
                Id: item.Id,
                Amount: item.Amount,
                Note: item.Note,
                CreatedAt: item.CreatedAt,
                Type: item.Category.Type,
                TypeName: item.Category.Type == TransactionTypeEnum.Income ? "收入" : "支出",
                CategoryId: item.CategoryId,
                CategoryName: item.Category.Name)
            ).ToListAsync();

        var paginationMeta = new PaginationMeta
        (
            Total: total,
            Page: req.Page,
            Limit: req.Limit,
            TotalPages: (int)Math.Ceiling((double)total / req.Limit)
        );
        return new PaginatedResponse<TransactionResponse>(
            Data: transactions,
            Meta: paginationMeta
        );
    }
    /// <summary>
    /// 取得交易明細類別列表
    /// </summary>
    /// <returns></returns>
    public List<SelectListDto> GetTransactionTypeList()
    {
        return Enum.GetValues<TransactionTypeEnum>()
            .Select(item => (
                new SelectListDto(
                    Title: item == TransactionTypeEnum.Income ? "收入" : "支出",
                    Value: ((int)item).ToString()
                )
            )).ToList();
    }
    public async Task DeleteTransaction(Guid id, Guid userId)
    {
        var transaction = await dbContext.Transactions.FirstOrDefaultAsync(item => item.UserId == userId && item.Id == id);
        if(transaction is null)
        {
            throw new ApiException("無此交易明細", 400);
        }
        dbContext.Transactions.Remove(transaction);
        await dbContext.SaveChangesAsync();
    }
}
