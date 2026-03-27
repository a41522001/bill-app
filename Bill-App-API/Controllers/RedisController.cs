using Bill_App_Cache.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bill_App_API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RedisController(IRedisService redis) : ControllerBase
{
    [HttpGet("redis-test")]
    public async Task<IActionResult> RedisTest()
    {
        await redis.StringSetAsync("test", "hello redis");
        var value = await redis.StringGetAsync("test");
        return Ok(value);
    }
}
