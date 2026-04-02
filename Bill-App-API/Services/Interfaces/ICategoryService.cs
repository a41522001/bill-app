using Bill_App_API.Dtos;
namespace Bill_App_API.Interfaces;

public interface ICategoryService
{
    Task AddCategory(CategoryAddRequest req, Guid userId);
    Task<List<CategoryResponse>> GetCategory(Guid userId);
    Task DeleteCategory(Guid userId, Guid categoryId);
}
