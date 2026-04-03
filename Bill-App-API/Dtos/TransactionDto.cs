namespace Bill_App_API.Dtos;

public record TransactionAddRequest
(
    Guid CategoryId,
    Decimal Amount,
    string? Note
);
