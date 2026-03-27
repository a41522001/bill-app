using Bill_App.Contexts;
using Bill_App.Dtos;
using Bill_App.Interfaces;
using Bill_App_Cache.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Bill_App.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController(ILogger<UserController> logger, BillDbContext dbContext, IUserService userService) : ControllerBase
{
    /// <summary>
    /// 註冊
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    [HttpPost("signup")]
    public async Task<ActionResult> SignUp([FromBody] UserSignupRequest req)
    {
        var isOk = await userService.Signup(req);
        if (isOk)
        {
            return Ok("註冊成功");
        }
        return BadRequest("註冊失敗");
    }

    /// <summary>
    /// 登入
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] UserLoginRequest req)
    {
        var user = await userService.Login(req);
        if (user is null)
        {
            return BadRequest("登入失敗");
        }
        return Ok("登入成功");
    }
    [HttpPost("userId")]
    public async Task<ActionResult> GetUserId([FromBody] Guid sub)
    {
        var userId = await userService.GetUserId(sub);
        if (userId is null)
        {
            return BadRequest("查詢失敗");
        }
        return Ok(userId);
    }
}
