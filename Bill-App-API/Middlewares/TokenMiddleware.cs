
using Bill_App.Interfaces;
using Bill_App.Models;
using Bill_App.Services.Interfaces;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Bill_App_Cache.Services;
namespace Bill_App_API.Middlewares;

public class TokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITokenService tokenService, RedisService RedisService, IUserService userService)
    {
        // 不處理登入和註冊的請求
        string[] witheList = {"/api/user/login", "/api/user/signup" }; 
        var path = context.Request.Path;
        if(witheList.Contains(path))
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
                    // TODO: 暫時先這樣，之後可以改成回傳401
                    throw new Exception("Invalid token");
                }
                var subGuid = Guid.Parse(subString!);
                var userinfo = await RedisService.GetUserSubAsync(subGuid);
                if(userinfo is null)
                {
                    var userId = await userService.GetUserId(subGuid);
                    if (userId is not null)
                    {
                        await RedisService.SetUserSubAsync(subGuid, new UserSubHash(
                            UserId: (Guid)userId,
                            Email: email,
                            Name: name
                        ));
                    }
                    else
                    {
                        // TODO: 暫時先這樣，之後可以改成回傳401
                        throw new Exception("Can't fetch userId");
                    }
                }
                context.Request.Headers["userId"] = userinfo.UserId.ToString();
            }
        }
        else
        {
            // Cookie裡沒有accessToken，直接丟給下一個middleware，讓它去處理（可能是refresh token的middleware）

        }
        var refreshToken = context.Request.Cookies["refreshToken"];
        await next(context);
    }
}
