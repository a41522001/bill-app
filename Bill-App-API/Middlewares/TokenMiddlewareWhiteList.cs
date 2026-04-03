namespace Bill_App_API.Middlewares;

public static class TokenMiddlewareWhiteList
{
    public static readonly string[] Routes =
    [
        "/api/user/login",
        "/api/user/signup",
        "/api/user/logout",
        "/api/user/verifyEmail",
        "/api/user/googleLogin",
        "/api/user/resendVerifyEmail"
    ];

    public static bool IsWhiteListed(PathString path)
    {
        foreach (var route in Routes)
        {
            if (path.StartsWithSegments(route))
            {
                return true;
            }
        }
        return false;
    }
}
