using Bill_App_Cache.Interface;
using Bill_App_Cache.Keys;
using Bill_App_Cache.Dtos;
using StackExchange.Redis;
namespace Bill_App_Cache.Services;
public class RedisService(IConnectionMultiplexer redis) : IRedisService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task StringSetAsync(string key, string data)
        => await _db.StringSetAsync(key, data);

    public async Task<string?> StringGetAsync(string key)
        => await _db.StringGetAsync(key);

    public async Task SetUserSubAsync(Guid sub, UserSubHash data)
    {
        var entries = new HashEntry[]
        {
            new HashEntry("UserId", data.UserId.ToString()),
            new HashEntry("Email", data.Email),
            new HashEntry("Name", data.Name),
        };
        await _db.HashSetAsync(RedisKeys.UserSub(sub), entries);
    }
    public async Task<UserSubHash?> GetUserSubAsync(Guid sub)
    {
        var entries = await _db.HashGetAllAsync(RedisKeys.UserSub(sub));
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
