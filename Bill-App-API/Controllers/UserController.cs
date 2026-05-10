using Bill_App_API.Dtos;
using Bill_App_API.Extensions;
using Bill_App_API.Interfaces;
using Bill_App_API.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Bill_App_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController(ILogger<UserController> logger, IUserService userService, IOptions<JwtOptions>
    jwtOptions, IOptions<RefreshTokenOptions> refreshTokenOptions, IOptions<AuthCookieOptions> authCookieOptions) : ControllerBase
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly RefreshTokenOptions _refreshTokenOptions = refreshTokenOptions.Value;
    private readonly AuthCookieOptions _authCookieOptions = authCookieOptions.Value;
    /// <summary>
    /// 重送驗證信
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    [HttpPost("resendVerifyEmail")]
    public async Task<ActionResult> ResendVerifyEmail([FromBody] UserResendVerifyEmailRequest req)
    {
        await userService.ResendVerifyEmail(req.Email);
        return Ok("若該信箱已註冊，驗證信已寄出");
    }
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
            return Ok("註冊成功請至信箱收取驗證信");
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
        Response.Cookies.Append("accessToken", tokens.AccessToken,
            _authCookieOptions.Create(DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.DurationInMinutes)));
        Response.Cookies.Append("refreshToken", tokens.RefreshToken.ToString(),
            _authCookieOptions.Create(DateTimeOffset.UtcNow.AddDays(_refreshTokenOptions.DurationInDays)));
        return Ok("登入成功");
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
        if (refreshToken is not null)
        {
            await userService.Logout(refreshToken);
        }
        Response.Cookies.Delete("accessToken", _authCookieOptions.Create());
        Response.Cookies.Delete("refreshToken", _authCookieOptions.Create());
        return Ok("登出成功");
    }
    /// <summary>
    /// 取得使用者資訊
    /// </summary>
    /// <returns></returns>
    [HttpGet("profile")]
    public async Task<ActionResult> Profile()
    {
        var userId = HttpContext.GetUserId();
        var profile = await userService.GetProfile(userId);
        return Ok(profile);
    }
    /// <summary>
    /// Google 登入
    /// </summary>
    [HttpPost("googleLogin")]
    public async Task<ActionResult> GoogleLogin([FromBody] GoogleLoginRequest req)
    {
        var tokens = await userService.GoogleLogin(req.IdToken);
        Response.Cookies.Append("accessToken", tokens.AccessToken,
            _authCookieOptions.Create(DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.DurationInMinutes)));
        Response.Cookies.Append("refreshToken", tokens.RefreshToken.ToString(),
            _authCookieOptions.Create(DateTimeOffset.UtcNow.AddDays(_refreshTokenOptions.DurationInDays)));
        return Ok("Google 登入成功");
    }
    /// <summary>
    /// 驗證Email
    /// </summary>
    /// <returns></returns>
    [HttpGet("verifyEmail/{token:guid}")]
    public async Task<ActionResult> VerifyEmail(Guid token)
    {
        var isVerify = await userService.VerifyEmail(token);
        if (isVerify)
        {
            return Ok("驗證成功");
        }
        return BadRequest("驗證失敗");
    }
    /// <summary>
    /// 忘記密碼
    /// </summary>
    /// <returns></returns>
    [HttpPost("forgetPassword")]
    public async Task<ActionResult> ForgetPassword([FromBody] UserForgetPasswordRequest req)
    {
        await userService.ForgetPassword(req.Email);
        return Ok("若該信箱已註冊，重設密碼信已寄出");
    }
    /// <summary>
    /// 重設密碼
    /// </summary>
    /// <returns></returns>
    [HttpPost("resetPassword")]
    public async Task<ActionResult> ResetPassword([FromBody] UserResetPasswordRequest req)
    {
        await userService.ResetPassword(req);
        return Ok("重設密碼成功，請用新密碼登入");
    }
    /// <summary>
    /// 上傳頭像
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    [HttpPost("avatar")]
    public async Task<ActionResult> UploadAvatar([FromForm] IFormFile file)
    {
        var userId = HttpContext.GetUserId();
        await userService.UploadAvatar(file, userId);
        return Ok("上傳成功");
    }
    /// <summary>
    /// 修改密碼(登入狀態下)
    /// </summary>
    /// <param name=""></param>
    /// <returns></returns>
    [HttpPut("password")]
    public async Task<ActionResult> ChangePassword([FromBody] UserChangePasswordRequest req)
    {
        var userId = HttpContext.GetUserId();
        await userService.ChangePassword(req, userId);
        Response.Cookies.Delete("accessToken", _authCookieOptions.Create());
        Response.Cookies.Delete("refreshToken", _authCookieOptions.Create());
        return Ok("修改成功，請重新登入");
    }
}
