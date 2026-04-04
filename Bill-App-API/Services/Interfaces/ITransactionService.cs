using Bill_App_API.Dtos;

namespace Bill_App_API.Interfaces;

public interface ITransactionService
{
    Task AddTransaction(TransactionAddRequest req, Guid userId);
    Task<PaginatedResponse<TransactionResponse>> GetTransaction(TransactionQueryRequest req, Guid userId);
    List<SelectListDto> GetTransactionTypeList();
    Task DeleteTransaction(Guid id, Guid userId);
    Task UpdateTransaction(TransactionUpdateRequest req, Guid userId);
}
