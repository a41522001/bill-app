using Bill_App.Contexts;
using Bill_App.Dtos;
using Bill_App.Interfaces;
using Bill_App.Models;
using Bill_App.Utils;
using Microsoft.EntityFrameworkCore;
namespace Bill_App.Services;

public class UserService : IUserService
{
  private readonly BillDbContext _dbContext;
  public UserService(BillDbContext dbContext)
  {
    _dbContext = dbContext;
  }
  public async Task<bool> Signup(UserSignupRequest req)
  {
    var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Email == req.Email);
    if (user is null)
    {
      var hashPassword = PasswordHasher.HashPassword(req.Password);
      User newUser = new User
      {
        Name = req.Name,
        Email = req.Email,
        Password = hashPassword
      };
      await _dbContext.Users.AddAsync(newUser);
      await _dbContext.SaveChangesAsync();
      return true;

    }
    return false;
  }
  public async Task<User?> Login(UserLoginRequest req)
  {
    var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Email == req.Email);
    if (user is null)
    {
      return user;
    }
    bool isVerify = PasswordHasher.VerifyPassword(req.Password, user.Password);
    return isVerify ? user : null;
  }
  public async Task<Guid?> GetUserId(Guid sub)
  {
    var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Sub == sub);
    if (user is null)
    {
      return null;
    }
    return user.Id;
  }
}

