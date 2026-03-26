using Microsoft.AspNetCore.Mvc;
using Bill_App.Contexts;
using Bill_App.Dtos;
using Bill_App.Interfaces;

namespace Bill_App.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
  private readonly BillDbContext _dbContext;
  private readonly IUserService _userService;
  private readonly ILogger<UserController> _logger;
  public UserController(ILogger<UserController> logger, BillDbContext dbContext, IUserService userService)
  {
    _logger = logger;
    _dbContext = dbContext;
    _userService = userService;
  }

  /// <summary>
  /// 註冊
  /// </summary>
  /// <param name="req"></param>
  /// <returns></returns>
  [HttpPost("signup")]
  public async Task<ActionResult> SignUp([FromBody] UserSignupRequest req)
  {
    var isOk = await _userService.Signup(req);
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
    var user = await _userService.Login(req);
    if (user is null)
    {
      return BadRequest("登入失敗");
    }
    return Ok("登入成功");
  }
}
