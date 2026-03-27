using Bill_App.Contexts;
using Bill_App.Dtos;
using Bill_App.Interfaces;
using Bill_App.Models;
using Bill_App.Services.Interfaces;
using Bill_App.Utils;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Microsoft.EntityFrameworkCore;
namespace Bill_App.Services;

public class UserService(BillDbContext dbContext, IRedisService RedisService, ITokenService TokenService) : IUserService
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
            // 生成redis的user sub hash資訊 & zset ，過期時間為7天
            var expireAt = DateTime.UtcNow.AddDays(7);
            // 生成access token
            var accessToken = TokenService.GenerateAccessToken(user);
            // 生成refresh token
            var refreshToken = TokenService.GenerateRefreshToken();
            // 建立redis的user hash資訊
            await RedisService.SetUserSubAsync(user.Sub, userSub);
            // 建立redis的refresh token zset
            await RedisService.SetRefreshToken(refreshToken, new RefreshTokenHash(
                UserId: user.Id,
                Expire: expireAt.ToString("o"),
                Sub: user.Sub,
                Name: user.Name,
                IsOld: 0
            ));
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

