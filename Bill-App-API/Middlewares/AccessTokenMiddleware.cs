using Bill_App_API.Interfaces;
using Bill_App_API.Options;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Microsoft.Extensions.Options;
namespace Bill_App_API.Middlewares;

public class AccessTokenMiddleware(RequestDelegate next, IOptions<JwtOptions> jwtOptions, IOptions<RefreshTokenOptions> refreshTokenOptions, IOptions<UserCacheOptions> userCacheOptions)
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly RefreshTokenOptions _refreshTokenOptions = refreshTokenOptions.Value;
    private readonly UserCacheOptions _userCacheOptions = userCacheOptions.Value;
    public async Task InvokeAsync(HttpContext context, ITokenService tokenService, IRedisService redisService, IUserService userService)
    {
        // 不處理登入和註冊的請求
        string[] whiteList = { "/api/user/login", "/api/user/signup", "/api/user/logout" };
        var path = context.Request.Path;
        if(whiteList.Contains(path))
        {
            await next(context);
            return;
        }

        var accessToken = context.Request.Cookies["accessToken"];
        if(accessToken is not null)
        {
            var accessTokenValidatedResult = tokenService.ValidateAccessToken(accessToken);
            if(accessTokenValidatedResult is not null)
            {
                var subString = accessTokenValidatedResult.FindFirst("sub")?.Value;
                var email = accessTokenValidatedResult.FindFirst("email")?.Value;
                var name = accessTokenValidatedResult.FindFirst("name")?.Value;
                if(subString is null || email is null || name is null)
                {
                    // TODO: 之後SameSite要改成SameSiteMode.Strict
                    context.Response.Cookies.Delete("accessToken", new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.DurationInMinutes)
                    });
                    // TODO: 之後SameSite要改成SameSiteMode.Strict
                    context.Response.Cookies.Delete("refreshToken", new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = DateTimeOffset.UtcNow.AddDays(_refreshTokenOptions.DurationInDay)
                    });
                    // TODO: 暫時先這樣，之後可以改成回傳401
                    throw new Exception("Invalid token");
                }
                var subGuid = Guid.Parse(subString!);
                var userinfo = await redisService.GetUserSubAsync(subGuid);
                Guid? userId = userinfo?.UserId;
                if (userinfo is null)
                {
                    userId = await userService.GetUserId(subGuid);
                    if (userId is not null)
                    {
                        await redisService.SetUserSubAsync(subGuid, new UserSubHash(
                            UserId: (Guid)userId,
                            Email: email,
                            Name: name
                        ), TimeSpan.FromHours(_userCacheOptions.TtlInHours));
                    }
                    else
                    {
                        // TODO: 之後SameSite要改成SameSiteMode.Strict
                        context.Response.Cookies.Delete("accessToken", new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.None,
                            Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.DurationInMinutes)
                        });
                        // TODO: 之後SameSite要改成SameSiteMode.Strict
                        context.Response.Cookies.Delete("refreshToken", new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.None,
                            Expires = DateTimeOffset.UtcNow.AddDays(_refreshTokenOptions.DurationInDay)
                        });
                        // TODO: 暫時先這樣，之後可以改成回傳401
                        throw new Exception("Can't fetch userId");
                    }
                }
                context.Items["userId"] = userId;
            }
        }
        await next(context);
    }
}
