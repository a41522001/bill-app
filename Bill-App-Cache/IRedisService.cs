using Bill_App_Cache.Dtos;
namespace Bill_App_Cache.Interface;

public interface IRedisService
{
    Task StringSetAsync(string key, string data);
    Task<string?> StringGetAsync(string key);
    Task SetUserSubAsync(Guid sub, UserSubHash data);
    Task<UserSubHash?> GetUserSubAsync(Guid sub);
}