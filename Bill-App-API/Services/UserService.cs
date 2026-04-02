using Bill_App_API.Contexts;
using Bill_App_API.Dtos;
using Bill_App_API.Interfaces;
using Bill_App_API.Models;
using Bill_App_API.Utils;
using Bill_App_API.Options;
using Bill_App_API.Enums;
using Bill_App_API.Exceptions;
using Bill_App_Cache.Dtos;
using Bill_App_Cache.Interface;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bill_App_API.Services;

public class UserService(BillDbContext dbContext, IRedisService redisService, ITokenService tokenService, IEmailService emailService,
    IOptions<MaxDeviceOptions> maxDeviceOptions, IOptions<RefreshTokenOptions> refreshTokenOptions, IOptions<UserCacheOptions> userCacheOptions,
    IOptions<UserVerifyEmailOptions> userVerifyEmailOptions, IOptions<AppOptions> appOptions, IOptions<GoogleAuthOptions> googleAuthOptions,
    IOptions<FrontendOptions> frontendOptions) : IUserService
{
    private readonly RefreshTokenOptions _refreshTokenOptions = refreshTokenOptions.Value;
    private readonly UserCacheOptions _userCacheOptions = userCacheOptions.Value;
    private readonly UserVerifyEmailOptions _userVerifyEmailOptions = userVerifyEmailOptions.Value;
    private readonly AppOptions _appOptions = appOptions.Value;
    private readonly GoogleAuthOptions _googleAuthOptions = googleAuthOptions.Value;
    private readonly FrontendOptions _frontendOptions = frontendOptions.Value;
    /// <summary>
    /// 註冊
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    public async Task<bool> Signup(UserSignupRequest req)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == req.Email);
        if (user is null)
        {
            var hashPassword = PasswordHasher.HashPassword(req.Password);
            User newUser = new User
            {
                Name = req.Name,
                Email = req.Email,
                Password = hashPassword,
                AuthProvider = AuthProviderEnum.Local,
                IsEmailVerified = false
            };
            await dbContext.Users.AddAsync(newUser);
            await dbContext.SaveChangesAsync();
            Guid token = Guid.NewGuid();
            await redisService.SetEmailVerifyTokenAsync(token, newUser.Id, TimeSpan.FromHours(_userVerifyEmailOptions.TtlInHours));
            // TODO: 發送驗證信，內容包含驗證連結，連結中帶有token參數 (先用Console log 記錄下來 prod環境再發驗證信)
            var url = $"{_frontendOptions.Url}/verifyEmail/{token}";
            await emailService.SendAsync(req.Email, "Bill App - 驗證你的帳號", $"<h3>歡迎註冊 Bill App</h3><p>請點擊下方連結驗證你的信箱：</p><a href=\"{url}\">點擊驗證</a><p>此連結將在 {_userVerifyEmailOptions.TtlInHours} 小時後失效。</p>");
            Console.WriteLine($"[DEV] 驗證連結: {url}");
            return true;

        }
        return false;
    }
    /// <summary>
    /// 登出
    /// </summary>
    /// <param name="refreshToken"></param>
    /// <returns></returns>
    public async Task Logout(string refreshToken)
    {
        Guid refreshTokenGuid;
        bool isTransformCorrect = Guid.TryParse(refreshToken, out refreshTokenGuid);
        if(!isTransformCorrect)
        {
            return;
        }
        var uesrHash = await redisService.GetRefreshToken(refreshTokenGuid);
        if(uesrHash is null)
        {
            return;
        }
        await redisService.DeleteUserRefreshTokenByMember(uesrHash.UserId, refreshTokenGuid);
        await redisService.DeleteRefreshToken(refreshTokenGuid);
        return;
    }
    /// <summary>
    /// 登入
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    public async Task<UserLoginResponse> Login(UserLoginRequest req)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == req.Email);
        if (user is null)
        {
            throw new ApiException("帳號或密碼錯誤");
        }
        if (user.AuthProvider == AuthProviderEnum.Google)
        {
            throw new ApiException("該帳號已綁定 Google，請用 Google 登入");
        }
        bool isVerify = PasswordHasher.VerifyPassword(req.Password, user.Password);
        if (!isVerify)
        {
            throw new ApiException("帳號或密碼錯誤");
        }
        if (!user.IsEmailVerified)
        {
            throw new ApiException("信箱未驗證");
        }
        var userSub = new UserSubHash(
            UserId: user.Id,
            Email: user.Email,
            Name: user.Name
        );
        // 建立redis的user sub hash資訊 & zset，過期時間為設定天數
        var expireAt = DateTime.UtcNow.AddDays(_refreshTokenOptions.DurationInDay);
        // 產生access token
        var accessToken = tokenService.GenerateAccessToken(user.Name, user.Email, user.Sub);
        // 建立redis的user sub hash資訊
        await redisService.SetUserSubAsync(user.Sub, userSub, TimeSpan.FromHours(_userCacheOptions.TtlInHours));
        // 輪轉/建立 Refresh Token，並取得新的Refresh Token
        var refreshToken = await RotateRefreshToken(user.Id, expireAt);
        // 建立redis的refresh token hash資訊
        await redisService.SetRefreshToken(refreshToken, new RefreshTokenHash(
            UserId: user.Id,
            Email: user.Email,
            Expire: expireAt.ToString("o"),
            Sub: user.Sub,
            Name: user.Name,
            IsOld: IsOldType.No
        ), expireAt);
        return new UserLoginResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken
        ); 
    }
    /// <summary>
    /// 輪轉Refresh Token
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="expireAt"></param>
    /// <returns></returns>
    public async Task<Guid> RotateRefreshToken(Guid userId, DateTime expireAt)
    {
        // 產生refresh token
        var refreshToken = tokenService.GenerateRefreshToken();
        // 建立redis的refresh token zset
        var maxDevice = maxDeviceOptions.Value.MaxDevice;
        // 刪除存在Zset已過期的Refresh Token
        await redisService.DeleteExpiredUserRefreshTokens(userId);
        // 查詢Zset目前的數量
        var count = await redisService.GetUserRefreshTokenCount(userId);
        // 如果Zset的數量大於等於最大裝置數量就刪除最舊的一筆Refresh Token Hash跟資料也要刪除
        if (count >= maxDevice)
        {
            var oldRefreshToken = await redisService.PopOldestUserRefreshToken(userId);
            if (oldRefreshToken is not null)
            {
                await redisService.DeleteRefreshToken(Guid.Parse(oldRefreshToken));
            }
            else
            {
                // 如果沒有最舊的Refresh Token，就代表異常，這裡可以記log或是其他處理方式

            }
        }
        // 設置新的Refresh Token Zset
        await redisService.SetUserRefreshToken(userId, refreshToken, expireAt);
        return refreshToken;
    }
    /// <summary>
    /// 取得UserId(測試)
    /// </summary>
    /// <param name="sub"></param>
    /// <returns></returns>
    public async Task<Guid?> GetUserId(Guid sub)
    {
        var result = await redisService.GetUserSubAsync(sub);
        if (result is null)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Sub == sub);
            if (user is null)
            {
                return null;
            }
            return user.Id;
        }
        return result.UserId;
    }
    /// <summary>
    /// 驗證Email
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> VerifyEmail(Guid token)
    {
        var userId = await redisService.GetEmailVerifyUserId(token);
        if (userId is not null)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId);
            if (user is not null)
            {
                user.IsEmailVerified = true;
                dbContext.Users.Update(user);
                await dbContext.SaveChangesAsync();
                await redisService.DeleteEmailVerifyTokenAsync(token);
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// Google登入
    /// </summary>
    /// <param name="idToken"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public async Task<UserLoginResponse> GoogleLogin(string idToken)
    {
        // 驗證 Google ID Token
        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_googleAuthOptions.ClientId]
            });

        var email = payload.Email;
        var name = payload.Name;

        // 查詢 DB 該 Email 是否已存在
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);

        // 已存在 + Local 帳號 → 拒絕登入
        if (user is not null && user.AuthProvider == AuthProviderEnum.Local)
        {
            throw new ApiException("該 Email 已使用密碼註冊，請用密碼登入");
        }

        // 不存在 → 自動建立 Google 帳號
        if (user is null)
        {
            user = new User
            {
                Name = name,
                Email = email,
                Password = null,
                AuthProvider = AuthProviderEnum.Google,
                IsEmailVerified = true
            };
            await dbContext.Users.AddAsync(user);
            await dbContext.SaveChangesAsync();
        }

        // 產生 Token（與 Login 相同邏輯）
        var userSub = new UserSubHash(UserId: user.Id, Email: user.Email, Name: user.Name);
        var expireAt = DateTime.UtcNow.AddDays(_refreshTokenOptions.DurationInDay);
        var accessToken = tokenService.GenerateAccessToken(user.Name, user.Email, user.Sub);
        await redisService.SetUserSubAsync(user.Sub, userSub, TimeSpan.FromHours(_userCacheOptions.TtlInHours));
        var refreshToken = await RotateRefreshToken(user.Id, expireAt);
        await redisService.SetRefreshToken(refreshToken, new RefreshTokenHash(
            UserId: user.Id,
            Email: user.Email,
            Expire: expireAt.ToString("o"),
            Sub: user.Sub,
            Name: user.Name,
            IsOld: IsOldType.No
        ), expireAt);

        return new UserLoginResponse(AccessToken: accessToken, RefreshToken: refreshToken);
    }
}

