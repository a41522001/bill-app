using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Bill_App_Cache.Keys;
using StackExchange.Redis;
using System.Xml.Linq;
namespace Bill_App_Cache.Services;
public class RedisService(IConnectionMultiplexer redis) : IRedisService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task StringSetAsync(string key, string data)
        => await _db.StringSetAsync(key, data);

    public async Task<string?> StringGetAsync(string key)
        => await _db.StringGetAsync(key);
    
    public async Task SetUserRefreshToken(Guid userId, Guid refreshToken, DateTime expireAt)
    {
        var key = RedisKeys.UserRefreshTokens(userId);
        double score = new DateTimeOffset(expireAt).ToUnixTimeMilliseconds();
        await _db.SortedSetAddAsync(key, refreshToken.ToString(), score);
    }
    public async Task SetRefreshToken(Guid refreshToken, RefreshTokenHash data)
    {
        var key = RedisKeys.RefreshToken(refreshToken);
        var entries = new HashEntry[]
        {
            new HashEntry("UserId", data.UserId.ToString()),
            new HashEntry("Expire", data.Expire),
            new HashEntry("Sub", data.Sub.ToString()),
            new HashEntry("Name", data.Name),
            new HashEntry("IsOld", data.IsOld.ToString())
        };
        await _db.HashSetAsync(key, entries);
    }
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
            Sub: Guid.Parse(dict["Sub"]),
            Name: dict["Name"],
            IsOld: Enum.Parse<IsOldType>(dict["IsOld"])
         );
    }
    public async Task SetUserSubAsync(Guid sub, UserSubHash data)
    {
        var key = RedisKeys.UserSub(sub);
        var entries = new HashEntry[]
        {
            new HashEntry("UserId", data.UserId.ToString()),
            new HashEntry("Email", data.Email),
            new HashEntry("Name", data.Name),
        };
        await _db.HashSetAsync(key, entries);
    }
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
}
