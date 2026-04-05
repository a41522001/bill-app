using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Enums;
using Bill_App_API.Exceptions;
using Bill_App_API.Interfaces;
using Bill_App_API.Models;
using Microsoft.EntityFrameworkCore;
namespace Bill_App_API.Services;

public class StatisticsService(BillDbContext dbContext) : IStatisticsService
{
    public async Task<StatisticsResponse> GetStatistics(StatisticsQueryRequest req, Guid userId)
    {
        var query = dbContext.Transactions
            .Where(item => item.UserId == userId && item.CreatedAt >= req.StartDate && item.CreatedAt < req.EndDate);
        var result = await query
            .GroupBy(t => new { t.Category.Type, t.Category.Name })
            .Select(g => new
            {
                CategoryType = g.Key.Type,
                CategoryName = g.Key.Name,
                Total = g.Sum(t => t.Amount)
            })
            .ToListAsync();

        var incomeTotal = result
            .Where(item => item.CategoryType == TransactionTypeEnum.Income)
            .Sum(item => item.Total);
        var expenseTotal = result
            .Where(item => item.CategoryType == TransactionTypeEnum.Expense)
            .Sum(item => item.Total);

        var incomeItems = result
            .Where(r => r.CategoryType == TransactionTypeEnum.Income)
            .OrderByDescending(r => r.Total)
            .Select(r => new StatisticsItemResponse(
                CategoryName: r.CategoryName,
                Amount: r.Total,
                Percentage: incomeTotal > 0
                    ? Math.Round(r.Total / incomeTotal * 100, 2)
                    : 0
            ))
            .ToList();

        var expenseItems = result
            .Where(r => r.CategoryType == TransactionTypeEnum.Expense)
            .OrderByDescending(r => r.Total)
            .Select(r => new StatisticsItemResponse(
                CategoryName: r.CategoryName,
                Amount: r.Total,
                Percentage: expenseTotal > 0
                    ? Math.Round(r.Total / expenseTotal * 100, 2)
                    : 0
            ))
            .ToList();

        return new StatisticsResponse(
             Income: new StatisticsGroupResponse(Total: incomeTotal, Items: incomeItems),
             Expense: new StatisticsGroupResponse(Total: expenseTotal, Items: expenseItems)
         );
    }
}
