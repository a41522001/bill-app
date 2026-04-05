using Bill_App_API.Dtos;
namespace Bill_App_API.Interfaces;

public interface IStatisticsService
{
    Task<StatisticsResponse> GetStatistics(StatisticsQueryRequest req, Guid userId);
}
