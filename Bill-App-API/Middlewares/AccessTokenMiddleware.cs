using Bill_App_API.Exceptions;
using Bill_App_API.Interfaces;
using Bill_App_API.Options;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Microsoft.Extensions.Options;
namespace Bill_App_API.Middlewares;

public class AccessTokenMiddleware(RequestDelegate next, IOptions<UserCacheOptions> userCacheOptions)
{
    private readonly UserCacheOptions _userCacheOptions = userCacheOptions.Value;
    public async Task InvokeAsync(HttpContext context, ITokenService tokenService, IRedisService redisService, IUserService userService)
    {
        // 不處理Middleware的白名單 直接放行
        string[] whiteList = { "/api/user/login", "/api/user/signup", "/api/user/logout", "/api/user/verifyEmail", "/api/user/googleLogin" };
        var path = context.Request.Path;
        foreach (var item in whiteList)
        {
            if (path.StartsWithSegments(item))
            {
                await next(context);
                return;
            }
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
                    // 解JWT失敗 可能是被竄改
                    throw new ApiException("請重新登入", 401);
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
                        // userId為null 代表sub在資料庫中找不到對應的使用者 可能是JWT被竄改
                        throw new ApiException("請重新登入", 401);
                    }
                }
                context.Items["userId"] = userId;
            }
        }
        await next(context);
    }
}
