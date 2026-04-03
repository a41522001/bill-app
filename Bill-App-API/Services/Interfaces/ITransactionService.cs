using Bill_App_API.Dtos;

namespace Bill_App_API.Interfaces;

public interface ITransactionService
{
    Task AddTransaction(TransactionAddRequest req, Guid userId);
}
