using Bill_App_API.Dtos;
using Bill_App_API.Models;
namespace Bill_App_API.Interfaces;

public interface ICategoryService
{
  Task<bool> AddCategory(CategoryAddRequest req, Guid userId);
  Task<List<Category>> GetCategory(Guid userId);
}