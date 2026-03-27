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
}
