using Bill_App_API.Dtos;
using Bill_App_API.Exceptions;
using Bill_App_API.Options;
using Bill_App_Cache.Interface;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Bill_App_API.Middlewares;

public class LoginRateLimitMiddleware(RequestDelegate next, IOptions<LoginRateLimitOptions> loginRateLimitOptions)
{
    private readonly LoginRateLimitOptions _loginRateLimitOptions = loginRateLimitOptions.Value;
    public async Task InvokeAsync(HttpContext context, IRedisService redisService)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api/user/login") &&
            !path.StartsWithSegments("/api/user/googleLogin"))
        {
            await next(context);
            return;
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        if (ipAddress is null)
        {
            throw new ApiException("無法抓取IP", 400);
        }
        var expire = TimeSpan.FromMinutes(_loginRateLimitOptions.TtlMinute);
        var ipCount = await redisService.SetRateLimitLoginByIp(ipAddress, expire);
        var ipLimit = _loginRateLimitOptions.IpLimit;
        if (ipCount > ipLimit)
        {
            throw new ApiException("登入嘗試次數過多，請稍後再試", 429);
        }

        if (path.StartsWithSegments("/api/user/login"))
        {
            context.Request.EnableBuffering();
            var body = (context.Request.Body);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = await JsonSerializer.DeserializeAsync<UserLoginRequest>(body, options);
            context.Request.Body.Position = 0;
            if (data?.Email is not null)
            {
                var emailCount = await redisService.SetRateLimitLoginByEmail(data.Email, expire);
                var emailLimit = _loginRateLimitOptions.EmailLimit;
                if (emailCount > emailLimit)
                {
                    throw new ApiException("登入嘗試次數過多，請稍後再試", 429);
                }
            }
        }
        await next(context);
        return;
    }
}
