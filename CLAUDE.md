# Bill-App - Project Instructions

## Project Overview

Personal finance management API (記帳應用程式), built with ASP.NET Core 10 / C# 14.
Monorepo solution with two projects: `Bill-App-API` (Web API) and `Bill-App-Cache` (Redis class library).

## Tech Stack

- **Runtime**: .NET 10, C# 14
- **Database**: PostgreSQL 18.1 (via EF Core 10 + Npgsql)
- **Cache**: Redis 7.4 (via StackExchange.Redis)
- **Auth**: JWT (access token) + GUID (refresh token) + BCrypt password hashing + Email verification + Google OAuth (ID Token)
- **Google Auth**: Google.Apis.Auth (ID Token verification)
- **Email**: MailKit (SMTP via Gmail or other providers)
- **Infra**: Docker Compose (postgres + redis)

## Build & Run

```bash
# Start database and cache containers
docker compose up -d

# Run API (from project root)
dotnet run --project Bill-App-API

# Run with hot reload
dotnet watch run --project Bill-App-API

# Add EF Core migration
dotnet ef migrations add <MigrationName> --project Bill-App-API

# Apply migrations
dotnet ef database update --project Bill-App-API
```

- API runs on `http://localhost:5148` / `https://localhost:7188`
- Swagger UI available at `/swagger` in Development mode

## Project Structure

```
Bill-App-API/
├── Controllers/    # HTTP endpoints (UserController, CategoryController, TransactionController, RedisController)
├── Services/       # Business logic (UserService, TokenService, CategoryService, EmailService, TransactionService)
│   └── Interfaces/ # Service contracts (IUserService, ITokenService, ICategoryService, IEmailService, ITransactionService)
├── Middlewares/     # ExceptionHandlingMiddleware, AccessTokenMiddleware, RefreshTokenMiddleware, TokenMiddlewareWhiteList
├── Filters/        # LogActionFilter, ResultWrapFilter (GlobalExceptionFilter 已移至 ExceptionHandlingMiddleware)
├── Exceptions/     # ApiException (custom exception with StatusCode)
├── Options/        # JwtOptions, MaxDeviceOptions, RefreshTokenOptions, UserCacheOptions, UserVerifyEmailOptions, AppOptions, GoogleAuthOptions, SmtpOptions, FrontendOptions, AuthCookieOptions
├── Models/         # EF Core entities (User, Category, Transaction)
├── Dtos/           # Request/Response records + ResponseWrap<T>
├── Enums/          # TransactionTypeEnum, AuthProviderEnum (Local=0, Google=1), ResponseCodeEnum
├── Contexts/       # BillDbContext
├── Extensions/     # HttpContextExtension (GetUserId, HasUserId, SetUserId)
├── Utils/          # PasswordHasher (BCrypt wrapper)
├── Migrations/     # EF Core migrations
└── Program.cs      # DI registration, filters & middleware pipeline

docs/                # 專案文件與功能規劃
├── api-endpoints.md    # API Endpoints 總覽（所有 request/response 格式、ResponseCode、前端串接指南）
├── response-codes.md   # ResponseCodeEnum 完整定義
├── GoogleAuth.md       # Google OAuth 整合筆記
├── GmailSMTP.md        # Gmail SMTP 設定筆記
└── ResendAuthCode.md   # 重送驗證信功能規劃

Bill-App-Cache/  (namespace: Bill_App_Cache)
├── IRedisService.cs    # Redis service interface (Bill_App_Cache.Interface)
├── RedisService.cs     # Redis service implementation (Bill_App_Cache.Services)
├── RedisDto.cs         # Redis data records (Bill_App_Cache.Dtos)
└── RedisKey.cs         # Redis key patterns (Bill_App_Cache.Keys)
```

## Architecture & Conventions

- **Layered architecture**: Controller -> Service -> DbContext (no Repository layer)
- **DI lifetime**: All services registered as **Scoped**, `IConnectionMultiplexer` as **Singleton**
- **Namespace root**: `Bill_App_API` (API project), `Bill_App_Cache` (Cache project)
- **Interface prefix**: `I` (IUserService, ICategoryService, ITokenService)
- **DTOs**: Use C# `record` types for request objects
- **Models**: Return domain entities directly or use response DTOs (e.g. `UserProfileResponse`), no AutoMapper
- **HttpContext Extension**: C# 14 extension member 語法，提供 `GetUserId()`、`HasUserId()`、`SetUserId()` 統一管理 `context.Items["userId"]`
- **Async pattern**: All I/O operations must be async (`Task<T>`)
- **EF Core**: Code-first approach with explicit migrations
- **Nullable reference types**: Enabled project-wide
- **Options pattern**: Strongly-typed config via `IOptions<T>` (JwtOptions, MaxDeviceOptions, RefreshTokenOptions, UserCacheOptions, UserVerifyEmailOptions, AppOptions, GoogleAuthOptions, SmtpOptions, FrontendOptions, AuthCookieOptions)
- **Error handling**: `ApiException` for business errors (custom StatusCode + ResponseCode), `ExceptionHandlingMiddleware` 統一處理所有例外（Middleware + Controller），401 時自動清除 cookies
- **Token Middleware Whitelist**: `TokenMiddlewareWhiteList` 靜態類別集中管理白名單路由，提供 `IsWhiteListed(PathString)` 方法
- **CORS**: 允許前端 origin（`FRONT_END_URL` 環境變數），`AllowCredentials` 支援 cookie 跨域

## Authentication Flow

### Token Design

| Token | Type | Lifetime | Storage |
|-------|------|----------|---------|
| Access Token | JWT (HMAC SHA-256) | `JWT__DURATION_IN_MINUTES` (default: 15) | HttpOnly + Secure cookie |
| Refresh Token | GUID | `REFRESH_TOKEN__DURATION_IN_DAY` (default: 7) | HttpOnly + Secure cookie + Redis |

### Token Lifetime Configuration (Options Pattern)

| Options Class | Env Variable | Default | Description |
|---------------|-------------|---------|-------------|
| `JwtOptions` | `JWT__DURATION_IN_MINUTES` | 15 | Access Token (JWT) 有效分鐘數 |
| `RefreshTokenOptions` | `REFRESH_TOKEN__DURATION_IN_DAY` | 7 | Refresh Token 有效天數 |
| `RefreshTokenOptions` | `REFRESH_TOKEN__OLD_TOKEN_GRACE_IN_SECONDS` | 15 | 舊 RT 寬限秒數（併發請求容錯） |
| `UserCacheOptions` | `USER_CACHE__TTL_IN_HOURS` | 24 | UserSub Redis Hash TTL（安全網，搭配 write-through 更新） |
| `MaxDeviceOptions` | `MAX_DEVICE` | 5 | 每位用戶最大同時登入裝置數 |
| `UserVerifyEmailOptions` | `USER_VERIFY_EMAIL__TTL_IN_HOURS` | 1 | Email 驗證 token TTL |
| `AppOptions` | `APP__DOMAIN` | - | 應用程式 domain（用於產生驗證連結） |
| `GoogleAuthOptions` | `GOOGLE_AUTH_CLIENT_ID` | - | Google OAuth Client ID（ID Token 驗證用） |
| `SmtpOptions` | `SMTP_HOST` | - | SMTP 伺服器主機（如 smtp.gmail.com） |
| `SmtpOptions` | `SMTP_PORT` | 587 | SMTP 伺服器連接埠 |
| `SmtpOptions` | `SMTP_SENDER_EMAIL` | - | 寄件者 Email |
| `SmtpOptions` | `SMTP_SENDER_NAME` | - | 寄件者顯示名稱 |
| `SmtpOptions` | `SMTP_SENDER_PASSWORD` | - | 寄件者密碼（Gmail 需使用應用程式密碼） |
| `FrontendOptions` | `FRONT_END_URL` | - | 前端應用程式 URL（用於產生驗證信連結） |
| `AuthCookieOptions` | _(由環境決定)_ | `None` | Cookie SameSite 屬性（Production → `Strict`，其他 → `None`），提供 `Create()` factory 方法產生統一的 `CookieOptions` |

**Middleware 注入規則**：`IOptions<T>` 是 Singleton，放 constructor；Scoped 服務（ITokenService、IRedisService、IUserService）放 `InvokeAsync` 參數。

### Middleware Pipeline

```
Request → ExceptionHandlingMiddleware → AccessTokenMiddleware → RefreshTokenMiddleware → Controller
```

**ExceptionHandlingMiddleware** (最外層):
- 統一 catch 所有例外（取代原本的 `GlobalExceptionFilter`）
- `ApiException` → 用其 StatusCode + `ResponseWrap<object>.Error(message)`
- 其他 `Exception` → 500 + log + `ResponseWrap<object>.Error("伺服器內部錯誤")`
- 401 時自動清除 `accessToken` 和 `refreshToken` cookies

**Whitelist routes** (skip AccessToken & RefreshToken middlewares，定義於 `TokenMiddlewareWhiteList`): `/api/user/login`, `/api/user/signup`, `/api/user/logout`, `/api/user/verifyEmail`, `/api/user/googleLogin`, `/api/user/resendVerifyEmail`, `/api/user/forgetPassword`, `/api/user/resetPassword`

Whitelist 使用 `StartsWithSegments` 比對，支援動態路徑（如 `/api/user/verifyEmail/{token}`）。

**AccessTokenMiddleware**:
1. Read `accessToken` cookie → validate JWT (signature, issuer, audience, expiry)
2. Extract `sub` claim → query Redis (`user:sub#{sub}`) for cached user info
3. If cache miss → fallback to DB, then cache in Redis
4. Set `context.SetUserId(userId)` for downstream use

**RefreshTokenMiddleware** (only runs if `context.HasUserId()` is false):
1. Read `refreshToken` cookie → parse GUID → query Redis (`auth:refreshToken#{guid}`)
2. If `IsOld == Yes` → pass through (15s grace period for concurrent requests)
3. If `IsOld == No` → perform token rotation:
   - Remove old RT from ZSet **before** rotation (to avoid incorrect device count)
   - Generate new access token + new refresh token
   - Store new RT in Redis (ZSet + Hash)
   - Mark old RT as `IsOld=Yes` with 15s TTL
   - Set new cookies in response

### Redis Data Structures

| Key Pattern | Type | Purpose |
|------------|------|---------|
| `auth:refreshToken#{guid}` | Hash | RT data (UserId, Email, Sub, Name, Expire, IsOld) with TTL |
| `auth:user#{userId}:refreshToken` | ZSet | All RTs for a user, score = expiry timestamp (ms) |
| `user:sub#{sub}` | Hash | Cached user info (UserId, Email, Name) with TTL |
| `email:verify#{token}` | String | Email verification token → userId (GUID) with TTL |
| `email:resendCooldown#{userId}` | String | 重送驗證信冷卻（TTL 60s，防止短時間內重複請求） |
| `passwordReset#{token}` | String | 忘記密碼 token → userId (GUID) with TTL |
| `email:forgetCooldown#{userId}` | String | 忘記密碼信冷卻（TTL 60s，防止短時間內重複請求） |

### Multi-Device Support

- Max devices configurable via `MAX_DEVICE` env var (default: 5)
- On login/rotation: clean expired ZSet entries → check count → evict oldest if at limit
- `RotateRefreshToken()` in UserService handles device limit enforcement

### Signup Flow

1. Check email uniqueness
2. Create User (`AuthProvider = Local`, `IsEmailVerified = false`, password hashed with BCrypt)
3. Generate GUID verification token → store in Redis (`email:verify#{token}` = userId, TTL from config)
4. 透過 `EmailService` 寄送驗證信（MailKit SMTP），驗證連結指向**前端路由** `{FRONT_END_URL}/verifyEmail/{token}`，由前端取得 token 後呼叫後端驗證 API
5. 同時 Console.WriteLine 記錄驗證連結（方便開發除錯）

### Email Verification Flow

1. `GET /api/user/verifyEmail/{token:guid}` (whitelist route, no auth required)
2. Query Redis `email:verify#{token}` → get userId
3. Update DB `IsEmailVerified = true`
4. Delete Redis token (prevent reuse)

### Resend Verification Email Flow

1. `POST /api/user/resendVerifyEmail` (whitelist route, no auth required)
2. Request body: `UserResendVerifyEmailRequest { Email }`
3. 查詢 DB → 若 user 不存在、AuthProvider 非 Local、或已驗證 → 靜默返回（不洩漏帳號資訊）
4. 檢查 Redis `email:resendCooldown#{userId}` → 存在則靜默返回（60s 冷卻中，同樣不洩漏資訊）
5. 設定 `email:resendCooldown#{userId}`（TTL 60s）與產生新 GUID verification token 存入 Redis（`Task.WhenAll` 並行）
6. 透過 EmailService 寄送驗證信，連結指向前端路由 `{FRONT_END_URL}/verifyEmail/{token}`
7. Controller 統一回傳 `Ok("若該信箱已註冊，驗證信已寄出")`

### Forget Password Flow

1. `POST /api/user/forgetPassword` (whitelist route, no auth required)
2. Request body: `UserForgetPasswordRequest { Email }`
3. 查詢 DB → 若 user 不存在、信箱未驗證、或 AuthProvider 為 Google → 靜默返回（不洩漏帳號資訊）
4. 檢查 Redis `email:forgetCooldown#{userId}` → 存在則靜默返回（60s 冷卻中）
5. 設定 `email:forgetCooldown#{userId}`（TTL 60s）與產生新 GUID reset token 存入 Redis `passwordReset#{token}`（`Task.WhenAll` 並行）
6. 透過 EmailService 寄送重設密碼信，連結指向前端路由 `{FRONT_END_URL}/revisePassword/{token}`
7. Controller 統一回傳 `Ok("若該信箱已註冊，重設密碼信已寄出")`

### Reset Password Flow

1. `POST /api/user/resetPassword` (whitelist route, no auth required)
2. Request body: `UserResetPasswordRequest { Token, Password }`
3. Query Redis `passwordReset#{token}` → get userId，無效則拋 ApiException
4. 查 DB 取得 user，不存在則拋 ApiException
5. BCrypt hash 新密碼 → 更新 user.Password
6. 讀取該用戶所有 refresh token（ZSet）→ 並行刪除所有 RT hash（`Task.WhenAll`）
7. 並行刪除 reset token + 刪除整個 RT ZSet（`Task.WhenAll`）
8. 儲存 DB 變更（強制所有裝置重新登入）

### Login Flow (Local)

1. Verify credentials (email + bcrypt password)
2. Check `AuthProvider` — reject if `Google` (throw `ApiException`「該帳號已綁定 Google，請用 Google 登入」, `ResponseCodeEnum.AccountBoundToGoogle`)
3. Check `IsEmailVerified` — reject if `false` (throw `ApiException`, `ResponseCodeEnum.EmailNotVerified`)
4. Generate access token (JWT) + refresh token (GUID)
5. Store in Redis: UserSub hash, RT ZSet entry, RT hash
6. Set HttpOnly cookies for both tokens

### Google Login Flow

1. Frontend sends Google ID Token → `POST /api/user/googleLogin`
2. Verify ID Token via `GoogleJsonWebSignature.ValidateAsync()` (check signature + audience)
3. Extract email, name from payload
4. Query DB by email:
   - Email exists + `AuthProvider == Local` → reject (throw `ApiException`「該 Email 已使用密碼註冊，請用密碼登入」, `ResponseCodeEnum.AccountBoundToLocal`)
   - Email exists + `AuthProvider == Google` → proceed as login
   - Email not found → auto-create User (`AuthProvider = Google`, `IsEmailVerified = true`, `Password = null`)
5. Generate access token (JWT) + refresh token (GUID) — same as local login
6. Store in Redis + set HttpOnly cookies

### Account Conflict Rules

| Scenario | Result |
|----------|--------|
| Local login + AuthProvider is Google | Reject (請用 Google 登入, `ResponseCodeEnum.AccountBoundToGoogle`) |
| Google login + AuthProvider is Local | Reject (請用密碼登入, `ResponseCodeEnum.AccountBoundToLocal`) |
| Signup + Email already exists (any provider) | Reject (註冊失敗) |

### Logout Flow

1. Read `refreshToken` from cookie (skip middleware via whitelist)
2. Delete RT from ZSet (`DeleteUserRefreshTokenByMember`)
3. Delete RT hash (`DeleteRefreshToken`)
4. Delete both cookies

### Profile API

- `GET /api/user/profile` (requires auth)
- 透過 `HttpContext.GetUserId()` 取得 userId → 查 DB → 回傳 `UserProfileResponse`（Name, Email, AuthProvider, IsEmailVerified）
- 前端登入後呼叫此 API 取得使用者資訊，存入 Pinia auth store

## Category API

- `POST /api/category` — 新增類別（Name, Type），驗證同 user 不可重複類別名稱
- `GET /api/category` — 取得該使用者所有未刪除的類別，回傳 `CategoryResponse`（Id, Name, TypeName, Type）
- `DELETE /api/category/{id}` — 軟刪除類別（設定 `DeletedAt` 時間戳）
- Category 有 `TransactionTypeEnum Type`（Income=0 / Expense=1），TypeName 為中文對照（"收入" / "支出"）

## Transaction API

- `POST /api/transaction` — 新增交易記錄（CategoryId, Amount, Note），驗證 CategoryId 是否屬於該使用者
- `GET /api/transaction` — 查詢交易明細（分頁 + 篩選），支援 Type、CategoryId、StartDate、EndDate、Page、Limit
- `GET /api/transaction/typeList` — 取得交易類型下拉選單（`SelectListDto[]`：Title + Value）
- `DELETE /api/transaction/{id}` — 硬刪除交易記錄，驗證交易是否屬於該使用者
- Transaction 不儲存 Type，透過 Navigation Property 從 Category 取得 Type
- 查詢回傳 `PaginatedResponse<TransactionResponse>`，包含 Data + Meta（Total, Page, Limit, TotalPages）
- 時間篩選由前端傳 UTC DateTime（ISO 8601 格式）

## Environment Variables

Required in `.env` (loaded via DotNetEnv):

```
DB_USER=<postgres user>
DB_PASSWORD=<postgres password>
DB_NAME=<database name>
JWT__KEY=<at least 32 characters>
JWT__ISSUER=<issuer name>
JWT__AUDIENCE=<audience name>
JWT__DURATION_IN_MINUTES=<access token lifetime, default 15>
REFRESH_TOKEN__DURATION_IN_DAY=<refresh token lifetime in days, default 7>
REFRESH_TOKEN__OLD_TOKEN_GRACE_IN_SECONDS=<old RT grace period, default 15>
USER_CACHE__TTL_IN_HOURS=<user sub hash TTL, default 24>
MAX_DEVICE=<max concurrent devices per user, default 5>
USER_VERIFY_EMAIL__TTL_IN_HOURS=<email verify token TTL, default 1>
APP__DOMAIN=<application domain, e.g. https://localhost:7188>
GOOGLE_AUTH_CLIENT_ID=<Google OAuth Client ID>
FRONT_END_URL=<frontend origin, e.g. http://localhost:5173>
SMTP_HOST=<SMTP server host, e.g. smtp.gmail.com>
SMTP_PORT=<SMTP port, default 587>
SMTP_SENDER_EMAIL=<sender email address>
SMTP_SENDER_NAME=<sender display name>
SMTP_SENDER_PASSWORD=<sender password or app password>
```

## Important Notes

- **Ignore `bin/` and `obj/` folders** when scanning or searching the codebase
- `.env` files are gitignored - never commit secrets
- `.github/workflows/` exists but has no CI/CD pipelines yet
- Controllers 使用 `HttpContext.GetUserId()` extension 取得已驗證的 userId（不使用 `[Authorize]` attribute）
- Middleware 使用 `context.SetUserId()` / `context.HasUserId()` 操作 userId
- Cookie 設定統一透過 `AuthCookieOptions.Create()` 產生（HttpOnly, Secure, SameSite 依環境切換）
- Token-related business logic lives in `UserService`, JWT cryptography in `TokenService`
- `RedisController` (`GET /api/redis/redis-test`) 為 Redis 連線測試端點

## Filters (Global)

Registered in `Program.cs` via `AddControllers(options => options.Filters.Add<T>())`:

| Filter | Type | Purpose |
|--------|------|---------|
| `LogActionFilter` | IActionFilter | Logs controller/action name and arguments |
| `ResultWrapFilter` | IResultFilter | Wraps all responses in `ResponseWrap<T>` (skips if already wrapped) |

> Note: `GlobalExceptionFilter` 已移至 `ExceptionHandlingMiddleware`，統一處理 Middleware 和 Controller 層的例外。

### ResponseWrap\<T\>

Unified response envelope (`Bill_App_API.Dtos.ResponseWrap<T>`):

```csharp
{ Data: T?, Code: int, Message: string, Time: DateTime }
// Static factories: ResponseWrap<T>.Success(data), ResponseWrap<T>.Error(message)
```

### ResponseCodeEnum

定義於 `Bill_App_API.Enums.ResponseCodeEnum`，完整文件見 `docs/response-codes.md`。

| 範圍 | 模組 | 說明 |
|------|------|------|
| 0–999 | General | 通用（0=Success, 1=Error） |
| 1001–1999 | Auth | 驗證 / 登入相關 |
| 2001–2999 | Category | 類別相關 |
| 3001–3999 | Transaction | 交易相關 |

目前已定義的 Auth codes：

| Code | 常數名稱 | 說明 |
|------|----------|------|
| 1001 | `EmailNotVerified` | 信箱未驗證（前端顯示重送驗證信按鈕） |
| 1002 | `AccountBoundToGoogle` | 該帳號已綁定 Google |
| 1003 | `AccountBoundToLocal` | 該 Email 已使用密碼註冊 |
