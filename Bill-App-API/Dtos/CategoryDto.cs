using Bill_App_API.Enums;
namespace Bill_App_API.Dtos;

public record CategoryAddRequest(
  string Name,
  TransactionTypeEnum Type
);
