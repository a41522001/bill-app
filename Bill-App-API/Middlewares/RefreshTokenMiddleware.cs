using Bill_App_API.Interfaces;
using Bill_App_API.Models;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Bill_App_Cache.Services;
using System.Data.SqlTypes;

namespace Bill_App_API.Middlewares;

public class RefreshTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITokenService tokenService, IRedisService redisService, IUserService userService)
    {
        // 不處理登入和註冊的請求
        string[] witheList = { "/api/user/login", "/api/user/signup" };
        var path = context.Request.Path;
        if (witheList.Contains(path))
        {
            await next(context);
            return;
        }
        var userId = context.Items["userId"];
        if(userId is not null)
        {
            await next(context);
            return;
        }
        var refreshTokenString = context.Request.Cookies["refreshToken"];
        if (refreshTokenString is null)
        {
            // Cookie內沒有Refresh Token
            // TODO: 暫時先這樣，之後可以改成回傳401
            throw new Exception("無法取得Refresh Token 重新登入");
        }
        var refreshToken = Guid.Parse(refreshTokenString);
        var userinfo = await redisService.GetRefreshToken(refreshToken);
        if(userinfo is null)
        {
            // Redis內沒有儲存的Refresh Token
            // TODO: 暫時先這樣，之後可以改成回傳401
            throw new Exception("無法取得Refresh Token 重新登入");
        }
        var name = userinfo.Name;
        var email = userinfo.Email;
        var sub = userinfo.Sub;
        var expireAt = DateTime.UtcNow.AddDays(7);
        // 創建新的Access Token
        var accessToekn = tokenService.GenerateAccessToken(name, email, sub);
        // 創建新的Refresh Token
        var newRefreshToken = tokenService.GenerateRefreshToken();
        // 建立redis的user sub hash資訊
        //await RedisService.SetUserSubAsync(user.Sub, userSub);
        // 建立redis的refresh token zset
        //await RedisService.SetUserRefreshToken(user.Id, refreshToken, expireAt);
        // 建立redis的user hash資訊
        //await RedisService.SetRefreshToken(refreshToken, new RefreshTokenHash(
        //    UserId: user.Id,
        //    Email: user.Email,
        //    Expire: expireAt.ToString("o"),
        //    Sub: user.Sub,
        //    Name: user.Name,
        //    IsOld: IsOldType.No
        //), expireAt);
    }
}
