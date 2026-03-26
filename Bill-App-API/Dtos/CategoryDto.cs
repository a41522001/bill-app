using Bill_App.Enums;
namespace Bill_App.Dtos;

public record CategoryAddRequest(
  string Name,
  TransactionTypeEnum Type
);
