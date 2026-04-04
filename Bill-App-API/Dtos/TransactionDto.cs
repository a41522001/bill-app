using Bill_App_API.Enums;
using Bill_App_API.Models;

namespace Bill_App_API.Dtos;

public record TransactionAddRequest
(
    Guid CategoryId,
    Decimal Amount,
    string? Note
);
public record TransactionQueryRequest
(
    TransactionTypeEnum? Type,
    Guid? CategoryId,
    DateTime? StartDate,
    DateTime? EndDate,
    int Page = 1,
    int Limit = 10
);
public record TransactionResponse
(
    Guid Id,
    Decimal Amount,
    string? Note,
    DateTime CreatedAt,
    TransactionTypeEnum Type,
    string TypeName,
    Guid CategoryId,
    string CategoryName
);

