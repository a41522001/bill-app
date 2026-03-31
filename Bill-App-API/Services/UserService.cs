using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Interfaces;
using Bill_App_API.Models;
using Bill_App_API.Utils;
using Bill_App_API.Options;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace Bill_App_API.Services;

public class UserService(BillDbContext dbContext, IRedisService redisService, ITokenService tokenService, IOptions<MaxDeviceOptions> maxDeviceOptions) : IUserService
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
    public async Task Logout(string refreshToken)
    {
        Guid refreshTokenGuid;
        bool isTransformCorrect = Guid.TryParse(refreshToken, out refreshTokenGuid);
        if(!isTransformCorrect)
        {
            return;
        }
        var uesrHash = await redisService.GetRefreshToken(refreshTokenGuid);
        if(uesrHash is null)
        {
            return;
        }
        await redisService.DeleteUserRefreshTokenByMember(uesrHash.UserId, refreshTokenGuid);
        await redisService.DeleteRefreshToken(refreshTokenGuid);
        return;
    }
    public async Task<UserLoginResponse?> Login(UserLoginRequest req)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == req.Email);
        if (user is not null)
        {
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
                var accessToken = tokenService.GenerateAccessToken(user.Name, user.Email, user.Sub);
                // 建立redis的user sub hash資訊
                await redisService.SetUserSubAsync(user.Sub, userSub);
                // 輪轉/產生 Refresh Token，並取得新的Refresh Token
                var refreshToken = await RotateRefreshToken(user.Id, expireAt);
                // 建立redis的user hash資訊
                await redisService.SetRefreshToken(refreshToken, new RefreshTokenHash(
                    UserId: user.Id,
                    Email: user.Email,
                    Expire: expireAt.ToString("o"),
                    Sub: user.Sub,
                    Name: user.Name,
                    IsOld: IsOldType.No
                ), expireAt);
                return new UserLoginResponse(
                    AccessToken: accessToken,
                    RefreshToken: refreshToken
                );
            }
        }
        return null;
    }
    public async Task<Guid> RotateRefreshToken(Guid userId, DateTime expireAt)
    {
        // 生成refresh token
        var refreshToken = tokenService.GenerateRefreshToken();
        // 建立redis的refresh token zset
        var maxDevice = maxDeviceOptions.Value.MaxDevice;
        // 刪除存在Zset已過期的Refresh Token
        await redisService.DeleteExpiredUserRefreshTokens(userId);
        // 查詢Zset目前的筆數
        var count = await redisService.GetUserRefreshTokenCount(userId);
        // 如果Zset的數量大於等於最大裝置數量就刪掉最舊的一個Refresh Token Hash的部分也要刪掉
        if (count >= maxDevice)
        {
            var oldRefreshToken = await redisService.PopOldestUserRefreshToken(userId);
            if (oldRefreshToken is not null)
            {
                await redisService.DeleteRefreshToken(Guid.Parse(oldRefreshToken));
            }
            else
            {
                // 如果沒有拿到最舊的Refresh Token，代表有異常，這邊可以選擇紀錄log或是其他處理方式

            }
        }
        // 設置新的Refresh Token Zset
        await redisService.SetUserRefreshToken(userId, refreshToken, expireAt);
        return refreshToken;
    }
    public async Task<Guid?> GetUserId(Guid sub)
    {
        var result = await redisService.GetUserSubAsync(sub);
        if (result is null)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Sub == sub);
            if (user is null)
            {
                return null;
            }
            return user.Id;
        }
        return result.UserId;
    }
}

