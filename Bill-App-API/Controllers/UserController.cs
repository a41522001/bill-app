using Bill_App_API.Dtos;
using Bill_App_API.Interfaces;
using Bill_App_API.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bill_App_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController(ILogger<UserController> logger, IUserService userService, IOptions<JwtOptions> jwtOptions, IOptions<RefreshTokenOptions> refreshTokenOptions) : ControllerBase
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly RefreshTokenOptions _refreshTokenOptions = refreshTokenOptions.Value;
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
            // TODO: 之後SameSite要改成SameSiteMode.Strict
            Response.Cookies.Append("accessToken", tokens.AccessToken, new CookieOptions { 
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.DurationInMinutes)
            });
            // TODO: 之後SameSite要改成SameSiteMode.Strict
            Response.Cookies.Append("refreshToken", tokens.RefreshToken.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(_refreshTokenOptions.DurationInDay)
            });
            return Ok("登入成功");
        }
        return BadRequest("登入失敗");

    }
    /// <summary>
    /// 登出
    /// </summary>
    /// <param></param>
    /// <returns></returns>
    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if(refreshToken is not null)
        {
            await userService.Logout(refreshToken);
        }
        // TODO: 之後SameSite要改成SameSiteMode.Strict
        Response.Cookies.Delete("accessToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.DurationInMinutes)
        });
        // TODO: 之後SameSite要改成SameSiteMode.Strict
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTimeOffset.UtcNow.AddDays(_refreshTokenOptions.DurationInDay)
        });
        return Ok("登出成功");
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
