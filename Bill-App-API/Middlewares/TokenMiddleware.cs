
using Bill_App.Services.Interfaces;

namespace Bill_App_API.Middlewares;

public class TokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITokenService tokenService)
    {
        var token = context.Request.Cookies["accessToken"];

        if (token is not null)
        {
            var principal = tokenService.ValidateToken(token);

            if (principal is not null)
            {
                context.User = principal;
            }
        }
        await next(context);
    }
}
