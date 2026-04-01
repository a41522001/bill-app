using Bill_App_Cache.Dtos;
namespace Bill_App_Cache.Interface;

public interface IRedisService
{
    Task StringSetAsync(string key, string data);
    Task<string?> StringGetAsync(string key);
    // �R��User�L����Refresh Token ZSet
    Task DeleteExpiredUserRefreshTokens(Guid userId);
    // ���oUser��Refresh Token ZSet�ƶq
    Task<int> GetUserRefreshTokenCount(Guid userId);
    // �]�mUser��Refresh Token ZSet
    Task SetUserRefreshToken(Guid userId, Guid refreshToken, DateTime expireAt);
    // �R��User��Refresh Token ZSet���¤@��
    Task<string?> PopOldestUserRefreshToken(Guid userId);
    // �R��User��Refresh Token ZSet(by member)
    Task DeleteUserRefreshTokenByMember(Guid userId, Guid refreshToken);
    // �R��Refresh Token��Hash
    Task DeleteRefreshToken(Guid refreshToken);
    // ���oRefresh Token��Hash
    Task<RefreshTokenHash?> GetRefreshToken(Guid refreshToken);
    // �ק�Refresh Token��Hash
    Task UpdateRefreshToken(Guid refreshToken, string field, string data);
    // �ק�Refresh Token��Hash�L���ɶ�
    Task UpdateRefreshTokenExpire(Guid refreshToken, TimeSpan newExpiry);
    // �]�mRefresh Token��Hash
    Task SetRefreshToken(Guid refreshToken, RefreshTokenHash data, DateTime expireAt);
    // �]�mSub��hash��T
    Task SetUserSubAsync(Guid sub, UserSubHash data, TimeSpan ttl);
    // ���oSub��hash��T
    Task<UserSubHash?> GetUserSubAsync(Guid sub);
}