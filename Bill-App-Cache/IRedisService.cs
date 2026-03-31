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
    // 刪除Refresh Token的Hash
    Task DeleteRefreshToken(Guid refreshToken);
    // 取得Refresh Token的Hash
    Task<RefreshTokenHash?> GetRefreshToken(Guid refreshToken);
    // 修改Refresh Token的Hash
    Task UpdateRefreshToken(Guid refreshToken, string field, string data);
    // 修改Refresh Token的Hash過期時間
    Task UpdateRefreshTokenExpire(Guid refreshToken, TimeSpan newExpiry);
    // 設置Refresh Token的Hash
    Task SetRefreshToken(Guid refreshToken, RefreshTokenHash data, DateTime expireAt);
    // 設置Sub的hash資訊
    Task SetUserSubAsync(Guid sub, UserSubHash data);
    // 取得Sub的hash資訊
    Task<UserSubHash?> GetUserSubAsync(Guid sub);
}