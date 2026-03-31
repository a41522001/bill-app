using Bill_App_API.Interfaces;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
namespace Bill_App_API.Middlewares;

public class AccessTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITokenService tokenService, IRedisService redisService, IUserService userService)
    {
        // 不處理登入和註冊的請求
        string[] witheList = { "/api/user/login", "/api/user/signup", "/api/user/logout" };
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
                        ));
                    }
                    else
                    {
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
