using Bill_App.Dtos;
using Bill_App.Models;
namespace Bill_App.Interfaces;

public interface IUserService
{
  Task<bool> Signup(UserSignupRequest req);
  Task<UserLoginResponse?> Login(UserLoginRequest req);
  Task<Guid?> GetUserId(Guid sub);
}