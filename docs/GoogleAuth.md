# Google 登入實作流程

## 整體架構

```
Frontend (SPA)                          Backend (API)
─────────────                          ──────────────
1. 使用者點擊 Google 登入按鈕
2. Google Identity Services SDK
   彈出 Google 登入視窗
3. 使用者授權後取得 ID Token
4. POST /api/user/google-login  ──────→ 5. 驗證 ID Token (Google.Apis.Auth)
   { idToken: "eyJhbG..." }            6. 從 payload 取得 email, name
                                        7. 查詢 DB 該 Email 是否已存在
                                           ├─ 已存在 + Local 帳號 → 拒絕登入（回傳錯誤）
                                           ├─ 已存在 + Google 帳號 → 視為登入
                                           └─ 不存在 → 自動建立新使用者
                                        8. 產生 Access Token (JWT) + Refresh Token (GUID)
                                        9. 存入 Redis (UserSub, RT Hash, RT ZSet)
←─────────────────────────────────────  10. Set-Cookie: accessToken, refreshToken
11. 登入完成，後續請求自動帶 cookie
```

## 後端實作步驟

### Step 1: 安裝套件

```bash
dotnet add Bill-App-API package Google.Apis.Auth
```

### Step 2: 新增 GoogleOptions

**檔案**: `Bill-App-API/Options/GoogleOptions.cs`

```csharp
namespace Bill_App_API.Options;

public class GoogleOptions
{
    public string ClientId { get; set; } = "";
}
```

### Step 3: 修改 Program.cs

```csharp
// 新增 Google Options
builder.Services.Configure<GoogleOptions>(options =>
{
    options.ClientId = Environment.GetEnvironmentVariable("GOOGLE__CLIENT_ID") ?? "";
});
```

### Step 4: 新增 DTO

**檔案**: `Bill-App-API/Dtos/UserDto.cs` (新增)

```csharp
public record GoogleLoginRequest(string IdToken);
```

### Step 5: 新增 Service 方法

**檔案**: `Bill-App-API/Services/Interfaces/IUserService.cs`

```csharp
Task<UserLoginResponse> GoogleLogin(string idToken);
```

**檔案**: `Bill-App-API/Services/UserService.cs`

注入 `IOptions<GoogleOptions>`，新增方法：

```csharp
public async Task<UserLoginResponse> GoogleLogin(string idToken)
{
    // 1. 驗證 Google ID Token
    var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
        new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [_googleOptions.ClientId]
        });

    var email = payload.Email;
    var name = payload.Name;

    // 2. 查詢 DB
    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);

    if (user is not null && user.AuthProvider == AuthProviderEnum.Local)
    {
        throw new ApiException("該 Email 已使用密碼註冊，請用密碼登入");
    }

    // 3. 不存在 → 自動建立
    if (user is null)
    {
        user = new User
        {
            Name = name,
            Email = email,
            Password = null,
            AuthProvider = AuthProviderEnum.Google,
            IsEmailVerified = true  // Google 已驗證 email
        };
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();
    }

    // 4. 產生 Token（複用現有邏輯）
    var userSub = new UserSubHash(UserId: user.Id, Email: user.Email, Name: user.Name);
    var expireAt = DateTime.UtcNow.AddDays(_refreshTokenOptions.DurationInDays);
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
```

### Step 6: 新增 Controller Endpoint

**檔案**: `Bill-App-API/Controllers/UserController.cs`

```csharp
[HttpPost("google-login")]
public async Task<ActionResult> GoogleLogin([FromBody] GoogleLoginRequest req)
{
    var tokens = await userService.GoogleLogin(req.IdToken);

    Response.Cookies.Append("accessToken", tokens.AccessToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.None,
        Expires = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.DurationInMinutes)
    });
    Response.Cookies.Append("refreshToken", tokens.RefreshToken.ToString(), new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.None,
        Expires = DateTimeOffset.UtcNow.AddDays(_refreshTokenOptions.DurationInDays)
    });

    return Ok("Google 登入成功");
}
```

### Step 7: Middleware 白名單

**檔案**: `Bill-App-API/Middlewares/AccessTokenMiddleware.cs`

```csharp
string[] whiteList = {
    "/api/user/login",
    "/api/user/signup",
    "/api/user/logout",
    "/api/user/verifyEmail",
    "/api/user/google-login"   // 新增
};
```

### Step 8: 環境變數

**`.env` 新增**:

```
GOOGLE__CLIENT_ID=<your-google-client-id>
```

需要到 [Google Cloud Console](https://console.cloud.google.com/) 建立 OAuth 2.0 Client ID（類型選「Web application」）。

## 帳號衝突處理規則

| 情境                               | 結果                                  |
| ---------------------------------- | ------------------------------------- |
| Email 不存在                       | 自動建立 Google 帳號，直接登入        |
| Email 存在 + AuthProvider = Google | 正常登入                              |
| Email 存在 + AuthProvider = Local  | 拒絕，回傳「該 Email 已使用密碼註冊」 |

## 前端串接（參考）

使用 Google Identity Services SDK：

```html
<script src="https://accounts.google.com/gsi/client" async></script>
```

```javascript
google.accounts.id.initialize({
    client_id: "YOUR_GOOGLE_CLIENT_ID",
    callback: async (response) => {
        // response.credential 就是 ID Token
        await fetch("/api/user/google-login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            credentials: "include",
            body: JSON.stringify({ idToken: response.credential }),
        });
    },
});

google.accounts.id.renderButton(document.getElementById("google-btn"), {
    theme: "outline",
    size: "large",
});
```
