using Bill_App_API.Exceptions;
using Bill_App_API.Interfaces;
using Bill_App_API.Options;
using Bill_App_API.Extensions;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Microsoft.Extensions.Options;

namespace Bill_App_API.Middlewares;

public class RefreshTokenMiddleware(RequestDelegate next, IOptions<JwtOptions> jwtOptions, IOptions<RefreshTokenOptions> refreshTokenOptions, IOptions<UserCacheOptions> userCacheOptions, IOptions<AuthCookieOptions> authCookieOptions)
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly RefreshTokenOptions _refreshTokenOptions = refreshTokenOptions.Value;
    private readonly UserCacheOptions _userCacheOptions = userCacheOptions.Value;
    private readonly AuthCookieOptions _authCookieOptions = authCookieOptions.Value;
    public async Task InvokeAsync(HttpContext context, ITokenService tokenService, IRedisService redisService, IUserService userService)
    {
        // 不處理Middleware的白名單 直接放行
        if (TokenMiddlewareWhiteList.IsWhiteListed(context.Request.Path))
        {
            await next(context);
            return;
        }
        if (context.HasUserId())
        {
            await next(context);
            return;
        }
        var refreshTokenString = context.Request.Cookies["refreshToken"];
        if (refreshTokenString is null)
        {
            // Cookie內沒有Refresh Token
            throw new ApiException("請重新登入", 401);
        }
        Guid refreshToken;
        bool isTransformCorrect = Guid.TryParse(refreshTokenString, out refreshToken);
        if (!isTransformCorrect)
        {
            // Refresh Token格式錯誤
            throw new ApiException("請重新登入", 401);
        }
        var userinfo = await redisService.GetRefreshToken(refreshToken);
        if (userinfo is null)
        {
            // Redis內沒有儲存的Refresh Token
            throw new ApiException("請重新登入", 401);
        }
        context.SetUserId(userinfo.UserId);
        // 檢查是否需要輪轉Refresh Token 舊Refresh Token直接放行(代表前端是使用Promise.all) 但如果是上傳檔案需要前端設置Timeout
        if (userinfo.IsOld == IsOldType.Yes)
        {
            await next(context);
            return;
        }
        var name = userinfo.Name;
        var email = userinfo.Email;
        Guid sub = userinfo.Sub;
        var expireAt = DateTime.UtcNow.AddDays(_refreshTokenOptions.DurationInDay);
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
        ), TimeSpan.FromHours(_userCacheOptions.TtlInHours));
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
        await redisService.UpdateRefreshTokenExpire(refreshToken, TimeSpan.FromSeconds(_refreshTokenOptions.OldTokenGraceInSeconds));
        // 在Cookie設置新的Access Token和Refresh Token
        context.Response.Cookies.Append("accessToken", newAccessToken,
            _authCookieOptions.Create(DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.DurationInMinutes)));
        context.Response.Cookies.Append("refreshToken", newRefreshToken.ToString(),
            _authCookieOptions.Create(DateTimeOffset.UtcNow.AddDays(_refreshTokenOptions.DurationInDay)));
        await next(context);
    }
}
