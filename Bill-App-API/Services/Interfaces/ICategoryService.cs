using Bill_App.Dtos;
using Bill_App.Models;
namespace Bill_App.Interfaces;

public interface ICategoryService
{
  Task<bool> AddCategory(CategoryAddRequest req, Guid userId);
  Task<List<Category>> GetCategory(Guid userId);
}