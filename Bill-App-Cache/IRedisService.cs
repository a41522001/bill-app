using Bill_App_Cache.Dtos;
namespace Bill_App_Cache.Interface;

public interface IRedisService
{
    Task StringSetAsync(string key, string data);
    Task<string?> StringGetAsync(string key);
    // 刪除User過期的Refresh Token ZSet
    Task DeleteExpiredUserRefreshTokens(Guid userId);
    // 取得User的Refresh Token ZSet數量
    Task<int> GetUserRefreshTokenCount(Guid userId);
    // 設置User的Refresh Token ZSet
    Task SetUserRefreshToken(Guid userId, Guid refreshToken, DateTime expireAt);
    // 刪除User的Refresh Token ZSet最舊一筆
    Task<string?> PopOldestUserRefreshToken(Guid userId);
    // 刪除User的Refresh Token ZSet(by member)
    Task DeleteUserRefreshTokenByMember(Guid userId, Guid refreshToken);
    // 取得User的Refresh Token ZSet
    Task<List<Guid>> GetUserAllRefreshToken(Guid userId);
    // 刪除User的Refresh Token ZSet
    Task DeleteUserRefreshToken(Guid userId);
    // 刪除Refresh Token的Hash
    Task DeleteRefreshToken(Guid refreshToken);
    // 取得Refresh Token的Hash
    Task<RefreshTokenHash?> GetRefreshToken(Guid refreshToken);
    // 修改Refresh Token的Hash過期時間
    Task UpdateRefreshToken(Guid refreshToken, string field, string data);
    // 修改Refresh Token的Hash過期時間
    Task UpdateRefreshTokenExpire(Guid refreshToken, TimeSpan newExpiry);
    // 設置Refresh Token的Hash
    Task SetRefreshToken(Guid refreshToken, RefreshTokenHash data, DateTime expireAt);
    // 設置Sub的hash資訊
    Task SetUserSubAsync(Guid sub, UserSubHash data, TimeSpan ttl);
    // 取得Sub的hash資訊
    Task<UserSubHash?> GetUserSubAsync(Guid sub);
    // 設置Email驗證的UserId(By random token)
    Task SetEmailVerifyTokenAsync(Guid token, Guid userId, TimeSpan ttl);
    // 取得Email驗證的UserId(By random token)
    Task<Guid?> GetEmailVerifyUserId(Guid token);
    // 刪除Email驗證的UserId(By random token)
    Task DeleteEmailVerifyTokenAsync(Guid token);
    // 設置Email重發驗證信的冷卻時間(By userId)
    Task SetEmailResendCooldown(Guid userId, TimeSpan expire);
    // 取得Email重發驗證信的冷卻時間(By userId)
    Task<bool> GetEmailResendCooldown(Guid userId);
    // 設置忘記密碼的token
    Task SetForgetPasswordToken(Guid token, Guid userId, TimeSpan ttl);
    // 取得忘記密碼的token對應的UserId
    Task<Guid?> GetForgetPasswordUserId(Guid token);
    // 刪除忘記密碼的token
    Task DeleteForgetPasswordToken(Guid token);
    // 設置Email重發忘記密碼的冷卻時間(By userId)
    Task SetForgetPasswordCooldown(Guid userId, TimeSpan expire);
    // 取得Email重發忘記密碼的冷卻時間(By userId)
    Task<bool> GetForgetPasswordCooldown(Guid userId);
    // 設置Rate Limit Login By IP 次數
    Task<long> SetRateLimitLoginByIp(string ip, TimeSpan expire);
    // 設置/設置/遞增 Rate Limit Login By Email 次數
     Task<long> SetRateLimitLoginByEmail(string email, TimeSpan expire);
}
