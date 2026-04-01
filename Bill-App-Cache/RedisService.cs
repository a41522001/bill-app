using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Bill_App_Cache.Keys;
using StackExchange.Redis;
namespace Bill_App_Cache.Services;
public class RedisService(IConnectionMultiplexer redis) : IRedisService
{
    private readonly IDatabase _db = redis.GetDatabase();
    // 
    public async Task StringSetAsync(string key, string data)
        => await _db.StringSetAsync(key, data);
    // 取得
    public async Task<string?> StringGetAsync(string key)
        => await _db.StringGetAsync(key);
    // 刪除User過期的Refresh Token ZSet
    public async Task DeleteExpiredUserRefreshTokens(Guid userId)
    {
        var key = RedisKeys.UserRefreshTokens(userId);
        double now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _db.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, now);
    }
    // 刪除User的Refresh Token ZSet最舊一筆
    public async Task<string?> PopOldestUserRefreshToken(Guid userId)
    {
        var key = RedisKeys.UserRefreshTokens(userId);
        var result = await _db.SortedSetPopAsync(key, Order.Ascending);
        return result?.Element.ToString();
    }
    // 刪除User的Refresh Token ZSet(by member)
    public async Task DeleteUserRefreshTokenByMember(Guid userId, Guid refreshToken)
    {
        var key = RedisKeys.UserRefreshTokens(userId);
        await _db.SortedSetRemoveAsync(key, refreshToken.ToString());
    }
    // 取得User的Refresh Token ZSet數量
    public async Task<int> GetUserRefreshTokenCount(Guid userId)
    {
        var key = RedisKeys.UserRefreshTokens(userId);
        var count = await _db.SortedSetLengthAsync(key);
        return (int)count;
    }
    // 設置User的Refresh Token ZSet
    public async Task SetUserRefreshToken(Guid userId, Guid refreshToken, DateTime expireAt)
    {
        var key = RedisKeys.UserRefreshTokens(userId);
        double score = new DateTimeOffset(expireAt).ToUnixTimeMilliseconds();
        await _db.SortedSetAddAsync(key, refreshToken.ToString(), score);
    }
    // 刪除Refresh Token的Hash
    public async Task DeleteRefreshToken(Guid refreshToken)
    {
        var key = RedisKeys.RefreshToken(refreshToken);
        await _db.KeyDeleteAsync(key);
    }
    // 設置Refresh Token的Hash
    public async Task SetRefreshToken(Guid refreshToken, RefreshTokenHash data, DateTime expireAt)
    {
        var key = RedisKeys.RefreshToken(refreshToken);
        var entries = new HashEntry[]
        {
            new HashEntry("UserId", data.UserId.ToString()),
            new HashEntry("Expire", data.Expire),
            new HashEntry("Email", data.Email),
            new HashEntry("Sub", data.Sub.ToString()),
            new HashEntry("Name", data.Name),
            new HashEntry("IsOld", data.IsOld.ToString())
        };
        await _db.HashSetAsync(key, entries);
        await _db.KeyExpireAsync(key, expireAt - DateTime.UtcNow);
    }
    // 取得Refresh Token的Hash
    public async Task<RefreshTokenHash?> GetRefreshToken(Guid refreshToken)
    {
        var key = RedisKeys.RefreshToken(refreshToken);
        var entries = await _db.HashGetAllAsync(key);
        if (entries.Length == 0)
        {
            return null;
        }
        var dict = entries.ToDictionary(
            e => e.Name.ToString(),
            e => e.Value.ToString()
        );
        return new RefreshTokenHash(
            UserId: Guid.Parse(dict["UserId"]),
            Expire: dict["Expire"],
            Email: dict["Email"],
            Sub: Guid.Parse(dict["Sub"]),
            Name: dict["Name"],
            IsOld: Enum.Parse<IsOldType>(dict["IsOld"])
         );
    }
    // 修改Refresh Token的Hash
    public async Task UpdateRefreshToken(Guid refreshToken, string field, string data)
    {
        var key = RedisKeys.RefreshToken(refreshToken);
        await _db.HashSetAsync(key, field, data);
    }
    // 修改Refresh Token的Hash過期時間
    public async Task UpdateRefreshTokenExpire(Guid refreshToken, TimeSpan newExpiry)
    {
        var key = RedisKeys.RefreshToken(refreshToken);
        await _db.KeyExpireAsync(key, newExpiry);
    }
    // 設置Sub的hash資訊
    public async Task SetUserSubAsync(Guid sub, UserSubHash data, TimeSpan ttl)
    {
        var key = RedisKeys.UserSub(sub);
        var entries = new HashEntry[]
        {
            new HashEntry("UserId", data.UserId.ToString()),
            new HashEntry("Email", data.Email),
            new HashEntry("Name", data.Name),
        };
        await _db.HashSetAsync(key, entries);
        await _db.KeyExpireAsync(key, ttl);
    }
    // 取得Sub的hash資訊
    public async Task<UserSubHash?> GetUserSubAsync(Guid sub)
    {
        var key = RedisKeys.UserSub(sub);
        var entries = await _db.HashGetAllAsync(key);
        if (entries.Length == 0) 
        {
            return null; 
        }

        var dict = entries.ToDictionary(
            e => e.Name.ToString(),
            e => e.Value.ToString()
        );

        return new UserSubHash(
            UserId: Guid.Parse(dict["UserId"]),
            Email: dict["Email"],
            Name: dict["Name"]
        );
    }
    // 設置Email驗證的UserId(By random token)
    public async Task SetEmailVerifyTokenAsync(Guid token, Guid userId, TimeSpan ttl)
    {
        var key = RedisKeys.EmailVerify(token);
        await _db.StringSetAsync(key, userId.ToString(), ttl);
    }
    // 取得Email驗證的UserId(By random token)
    public async Task<Guid?> GetEmailVerifyUserId(Guid token)
    {
        var key = RedisKeys.EmailVerify(token);
        var result = await _db.StringGetAsync(key);
        if (result.IsNull)
        {
            return null;
        }
        return Guid.Parse(result.ToString()); 
    }
    // 刪除Email驗證的UserId(By random token)
    public async Task DeleteEmailVerifyTokenAsync(Guid token)
    {
        var key = RedisKeys.EmailVerify(token);
        await _db.KeyDeleteAsync(key);
    }
}
