using Bill_App.Contexts;
using Bill_App.Dtos;
using Bill_App.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bill_App.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        var tokens = await userService.Login(req);
        if (tokens is not null)
        {
            Response.Cookies.Append("accessToken", tokens.AccessToken, new CookieOptions { 
                HttpOnly = true,
                Secure = true,
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });
            Response.Cookies.Append("refreshToken", tokens.RefreshToken.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
            return Ok("登入成功");
        }
        return BadRequest("登入失敗");

    }
    [HttpGet("profile")]
    public async Task<ActionResult> Profile()
    {
        //var userId = await userService.GetUserId(sub);
        //if (userId is null)
        //{
        //    return BadRequest("查詢失敗");
        //}
        //return Ok(userId);
        return Ok();
    }
}
