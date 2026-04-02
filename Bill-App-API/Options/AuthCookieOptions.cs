namespace Bill_App_API.Options;

public class AuthCookieOptions
{
    public SameSiteMode SameSite { get; set; } = SameSiteMode.None;

    public CookieOptions Create(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSite,
        Expires = expires
    };
}
