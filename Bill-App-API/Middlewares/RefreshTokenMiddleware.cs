using Bill_App_API.Interfaces;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;

namespace Bill_App_API.Middlewares;

public class RefreshTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITokenService tokenService, IRedisService redisService, IUserService userService)
    {
        // 不處理登入和註冊的請求
        string[] witheList = { "/api/user/login", "/api/user/signup", "/api/user/logout" };
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
        Guid refreshToken;
        bool isTransformCorrect = Guid.TryParse(refreshTokenString, out refreshToken);
        if(!isTransformCorrect)
        {
            // TODO: 暫時先這樣，之後可以改成回傳401
            throw new Exception("無法取得Refresh Token 重新登入");
        }
        var userinfo = await redisService.GetRefreshToken(refreshToken);
        if(userinfo is null)
        {
            // Redis內沒有儲存的Refresh Token
            // TODO: 暫時先這樣，之後可以改成回傳401
            throw new Exception("無法取得Refresh Token 重新登入");
        }
        context.Items["userId"] = userinfo.UserId;
        // 檢查是否需要輪轉Refresh Token 舊Refresh Token直接放行(代表前端是使用Promise.all) 但如果是上傳檔案需要前端設置Timeout
        if (userinfo.IsOld == IsOldType.Yes)
        {
            await next(context);
            return;
        }
        var name = userinfo.Name;
        var email = userinfo.Email;
        Guid sub = userinfo.Sub;
        var expireAt = DateTime.UtcNow.AddDays(7);
        // 刪除在Zset內的舊Refresh Token
        await redisService.DeleteUserRefreshTokenByMember(userinfo.UserId, refreshToken);
        // 創建新的Access Token
        var newAccessToken = tokenService.GenerateAccessToken(name, email, sub);
        // 輪轉Refresh Token
        Guid newRefreshToken = await userService.RotateRefreshToken(userinfo.UserId, expireAt);
        // 建立redis的user sub hash資訊
        await redisService.SetUserSubAsync(sub, new UserSubHash(
            UserId: userinfo.UserId,
            Email: email,
            Name: name
        ));
        // 建立redis的user hash資訊
        await redisService.SetRefreshToken(newRefreshToken, new RefreshTokenHash(
            UserId: userinfo.UserId,
            Email: email,
            Expire: expireAt.ToString("o"),
            Sub: sub,
            Name: name,
            IsOld: IsOldType.No
        ), expireAt);
        // 改變舊Refresh Token的狀態 (設定成舊的)
        await redisService.UpdateRefreshToken(refreshToken, "IsOld", IsOldType.Yes.ToString());
        // 改變舊Refresh Token的過期時間 設成15秒 (縮短過期時間)
        await redisService.UpdateRefreshTokenExpire(refreshToken, TimeSpan.FromSeconds(15));
        // 在cookie設置新的Access Token和Refresh Token
        // TODO: 之後SameSite要改成SameSiteMode.Strict
        context.Response.Cookies.Append("accessToken", newAccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddMinutes(15)
        });
        // TODO: 之後SameSite要改成SameSiteMode.Strict
        context.Response.Cookies.Append("refreshToken", newRefreshToken.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
        await next(context);
    }
}
