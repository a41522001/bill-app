using Bill_App.Contexts;
using Bill_App.Dtos;
using Bill_App.Interfaces;
using Bill_App.Models;
using Bill_App.Utils;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
namespace Bill_App.Services;

public class UserService(BillDbContext dbContext, IRedisService RedisService) : IUserService
{
    public async Task<bool> Signup(UserSignupRequest req)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == req.Email);
        if (user is null)
        {
            var hashPassword = PasswordHasher.HashPassword(req.Password);
            User newUser = new User
            {
                Name = req.Name,
                Email = req.Email,
                Password = hashPassword
            };
            await dbContext.Users.AddAsync(newUser);
            await dbContext.SaveChangesAsync();
            return true;

        }
        return false;
    }
    public async Task<User?> Login(UserLoginRequest req)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == req.Email);
        if (user is null)
        {
            return user;
        }
        bool isVerify = PasswordHasher.VerifyPassword(req.Password, user.Password);
        if (isVerify)
        {
            var userSub = new UserSubHash(
                UserId: user.Id,
                Email: user.Email,
                Name: user.Name
            );
            await RedisService.SetUserSubAsync(user.Sub, userSub);
            return user;
        }
        return null;
    }
    public async Task<Guid?> GetUserId(Guid sub)
    {
        var result = await RedisService.GetUserSubAsync(sub);
        if(result is null)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Sub == sub);
            if(user is null)
            {
                return null;
            }
            var userSub = new UserSubHash(
                UserId: user.Id,
                Email: user.Email,
                Name: user.Name
            );
            await RedisService.SetUserSubAsync(user.Sub, userSub);
            return user.Id;
        }
        return result?.UserId;
    }
}

