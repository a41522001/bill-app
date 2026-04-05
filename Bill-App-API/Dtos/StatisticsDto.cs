namespace Bill_App_API.Dtos;

public record StatisticsQueryRequest
(
    DateTime StartDate,
    DateTime EndDate
);

public record StatisticsItemResponse
(
    string CategoryName,
    decimal Amount,
    decimal Percentage
);

public record StatisticsGroupResponse
(
    decimal Total,
    List<StatisticsItemResponse> Items
);

public record StatisticsResponse
(
    StatisticsGroupResponse Income,
    StatisticsGroupResponse Expense
);
