using Bill_App_API.Dtos;
using Bill_App_API.Exceptions;

namespace Bill_App_API.Middlewares;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == 401)
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None
                };
                context.Response.Cookies.Delete("accessToken", cookieOptions);
                context.Response.Cookies.Delete("refreshToken", cookieOptions);
            }
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(ResponseWrap<object>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "發生未處理的例外");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(ResponseWrap<object>.Error("伺服器內部錯誤"));
        }
    }
}
