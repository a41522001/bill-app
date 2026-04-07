using System;
using System.Collections.Generic;
using System.Text;

namespace Bill_App_Cache.Keys;

public static class RedisKeys
{
    public static string RefreshToken(Guid refreshToken)
    => $"auth:refreshToken#{refreshToken}";
    public static string UserRefreshTokens(Guid userId)
        => $"auth:user#{userId}:refreshToken";
    public static string UserSub(Guid sub)
        => $"user:sub#{sub}";
    public static string EmailVerify(Guid token)
        => $"email:verify#{token}";
    public static string EmailResendCooldown(Guid userId)
        => $"email:resendCooldown#{userId}";
    public static string PasswordResetToken(Guid token)
        => $"passwordReset#{token}";
    public static string EmailPasswordForgetCooldown(Guid userId)
        => $"email:forgetCooldown#{userId}";
    public static string RateLimitLoginByIp(string ip)
        => $"rateLimit:login:ip#{ip}";
    public static string RateLimitLoginByEmail(string email)
        => $"rateLimit:login:email#{email}";
}
