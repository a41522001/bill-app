# Bill-App - Project Instructions

## Project Overview

Personal finance management API（記帳應用程式），以 ASP.NET Core 10 / C# 14 打造。
Monorepo 解決方案，包含三個 project：
- **Bill-App-API** — Web API 主專案
- **Bill-App-Cache** — Redis class library
- **Bill-App-Tests** — xUnit 單元測試

## Tech Stack

- **Runtime**：.NET 10、C# 14
- **Database**：PostgreSQL 18.1（透過 EF Core 10 + Npgsql）
- **Cache**：Redis 7.4（透過 StackExchange.Redis）
- **Auth**：JWT（access token）+ GUID（refresh token）+ BCrypt 密碼雜湊 + Email 驗證 + Google OAuth（ID Token）
- **Google Auth**：Google.Apis.Auth（ID Token 驗證）
- **Email**：MailKit（SMTP，透過 Gmail 或其他 SMTP provider）
- **Image Processing**：SixLabors.ImageSharp（resize + WebP 轉換）
- **File Storage**：抽象介面 `IFileStorageService`，雙實作可切換 — `LocalFileStorageService`（存 `wwwroot/avatars/`）/ `S3FileStorageService`（AWS S3 + CloudFront CDN，使用 `AWSSDK.S3`）
- **Logging**：Serilog（NuGet 已加入，包含 `Serilog.AspNetCore`、`Sinks.Console`、`Sinks.File`、`Sinks.Map`、`Enrichers.Environment`、`Enrichers.Thread`）— 配置策略見 `docs/Logging.md`，**Program.cs 尚未實際 wire up**（後續實作）
- **Infra**：Docker Compose（dev：postgres + redis；prod：postgres + redis + api）
- **Testing**：xUnit + Moq + EF Core InMemoryDatabase
- **CI**：GitHub Actions（push/PR to main 或 dev → restore → build → test）

## Build & Run

```bash
# Dev 模式：本機跑 API + 容器跑依賴
docker compose up -d                           # 啟動 Postgres + Redis
dotnet run --project Bill-App-API              # 跑 API（預設 ASPNETCORE_ENVIRONMENT=Development）
dotnet watch run --project Bill-App-API        # Hot reload

# Prod 模擬：整套用容器跑（目前仍在本機，尚未上雲端）
docker compose -f compose.prod.yml up -d --build

# EF Core migrations
dotnet ef migrations add <MigrationName> --project Bill-App-API
dotnet ef database update --project Bill-App-API   # Dev 才需要手動執行；Prod 由 Program.cs 啟動時自動 Migrate

# 測試
dotnet test Bill-App-Tests
```

- **Dev API**：`http://localhost:5148` / `https://localhost:7188`
- **Prod API（容器）**：`http://localhost:8080`
- **Swagger UI**：`/swagger`（僅 Development 模式）
- **Health check**：`GET /health`

## Project Structure

```
Bill-App-API/
├── Controllers/          # UserController, CategoryController, TransactionController, StatisticsController, RedisController
├── Services/             # 商業邏輯實作
│   ├── UserService.cs
│   ├── CategoryService.cs
│   ├── TransactionService.cs
│   ├── StatisticsService.cs
│   ├── TokenService.cs
│   ├── EmailService.cs
│   ├── LocalFileStorageService.cs
│   ├── S3FileStorageService.cs
│   └── Interfaces/       # IUserService, ICategoryService, ITransactionService, IStatisticsService, ITokenService, IEmailService, IFileStorageService
├── Middlewares/          # ExceptionHandling / LoginRateLimit / AccessToken / RefreshToken + TokenMiddlewareWhiteList
├── Filters/              # LogActionFilter, ResultWrapFilter
├── Exceptions/           # ApiException（自訂 StatusCode + Code）
├── Options/              # 強型別設定類別（13 個，見下方 Configuration Strategy）
├── Models/               # User, Category, Transaction, Avatar
├── Dtos/                 # Request/Response records + ResponseWrap<T> + PaginatedResponse<T> + SelectListDto
├── Enums/                # TransactionTypeEnum, AuthProviderEnum, ResponseCodeEnum
├── Contexts/             # BillDbContext
├── Extensions/           # HttpContextExtension（C# 14 extension members）
├── Utils/                # PasswordHasher（BCrypt wrapper）
├── Migrations/           # EF Core migrations（6 個）
├── wwwroot/avatars/      # Local 模式頭像儲存目錄（gitignored，僅保留 .gitkeep）
├── appsettings.json      # 不變動 + 非機密預設值
└── Program.cs            # DI 註冊、Options 載入、middleware pipeline

Bill-App-Cache/  (namespace: Bill_App_Cache)
├── IRedisService.cs      # Bill_App_Cache.Interface
├── RedisService.cs       # Bill_App_Cache.Services
├── RedisDto.cs           # UserSubHash, RefreshTokenHash records、IsOldType enum
└── RedisKey.cs           # Bill_App_Cache.Keys（9 個 key pattern）

Bill-App-Tests/  (namespace: Bill_App_Tests)
├── Services/
│   ├── UserServiceTest.cs          # 34 個 test cases（含 [Theory]）
│   ├── CategoryServiceTest.cs      # 4 個 test cases
│   └── TransactionServiceTest.cs   # 10 個 test cases
└── Practices/                      # 練習用，不納入正式測試範圍

docs/
├── api-endpoints.md      # 所有端點 request/response 規格
├── response-codes.md     # ResponseCodeEnum 完整定義
├── GoogleAuth.md         # Google OAuth 整合筆記
├── GmailSMTP.md          # Gmail SMTP 設定筆記
├── ResendAuthCode.md     # 重送驗證信流程
├── S3Storage.md          # AWS S3 + CloudFront 設定
├── docker-cd-guide.md    # Docker 生產環境部署指南
└── Logging.md            # Serilog 策略（尚未實作）

根目錄/
├── .env                  # Dev 機密（gitignored）
├── .env.production       # Prod 機密（gitignored）
├── .env.example          # 範本（commit 進 repo）
├── compose.yml           # Dev：Postgres + Redis
├── compose.prod.yml      # Prod：Postgres + Redis + API
├── Dockerfile            # Multi-stage build（SDK build → ASP.NET runtime）
├── .dockerignore
├── .editorconfig
├── .gitignore
├── Bill-App.sln
└── .github/workflows/ci.yml  # restore → build → test
```

## Configuration Strategy

**核心原則**：`appsettings.json` 放「不會變動 + 非機密」的預設值；`.env` 只放「機密 + 跨環境變動」的值。Dev 與 Prod **共用同一份 `appsettings.json`**（不使用 `appsettings.Development.json` / `appsettings.Production.json` 覆蓋）。

### 載入機制

1. `Program.cs:14-18` 使用 `DotNetEnv.Env.Load()` 從 cwd 的 `../` 載入 `.env`（dev 本機 `dotnet run` 時生效）
2. Docker container 透過 `compose.*.yml` 的 `env_file:` 注入環境變數（`.env` 給 dev 容器、`.env.production` 給 prod 容器）
3. `appsettings.json` 由 `IConfiguration` 自動載入
4. Program.cs 用 `builder.Configuration.GetSection("Xxx").GetValue<T>(...)` **手動**從 appsettings 讀取，用 `Environment.GetEnvironmentVariable(...)` 從環境變數讀取
5. **沒有使用 `Configure<T>(IConfigurationSection)` 自動 binding**——所有 Options 都在 Program.cs 顯式賦值（明確優於聰明）

### appsettings.json 的內容（共用，不變動）

| Section | Key | 預設值 | 對應 Options |
|---------|-----|--------|-------------|
| `JWT` | `Issuer` | `bill-app` | JwtOptions.Issuer |
| `JWT` | `Audience` | `bill-app` | JwtOptions.Audience |
| `JWT` | `DurationInMinutes` | `15` | JwtOptions.DurationInMinutes |
| `RefreshToken` | `DurationInDays` | `7` | RefreshTokenOptions.DurationInDays |
| `RefreshToken` | `OldTokenGraceInSeconds` | `15` | RefreshTokenOptions.OldTokenGraceInSeconds |
| `Device` | `MaxDevice` | `5` | MaxDeviceOptions.MaxDevice |
| `SMTP` | `Host` | `smtp.gmail.com` | SmtpOptions.Host |
| `SMTP` | `Port` | `587` | SmtpOptions.Port |
| `SMTP` | `SenderName` | `Bill App` | SmtpOptions.SenderName |
| `LoginRateLimit` | `IP` | `20` | LoginRateLimitOptions.IpLimit |
| `LoginRateLimit` | `Email` | `5` | LoginRateLimitOptions.EmailLimit |
| `LoginRateLimit` | `TtlMinutes` | `15` | LoginRateLimitOptions.TtlMinute |
| `UserCache` | `TtlInHours` | `24` | UserCacheOptions.TtlInHours |
| `UserVerifyEmailCache` | `TtlInHours` | `1` | UserVerifyEmailOptions.TtlInHours |

### .env 的內容（機密 + 跨環境變動）

```env
# Database（dev=localhost / prod=db hostname，由 compose.prod.yml 覆蓋成 db）
DB_USER=admin
DB_PASSWORD=thisispassword
DB_NAME=bill-app
DB_HOST=localhost
DB_PORT=5432

# Redis（同上，prod 由 compose.prod.yml 覆蓋成 redis:6379）
REDIS_CONNECTION=localhost:6379

# JWT 簽章 key（secret）
JWT_KEY=YourSuperSecretKeyAtLeast32Characters!

# 應用程式 URL（跨環境變動）
APP_DOMAIN=https://localhost:7188
FRONT_END_URL=http://localhost:5173

# Google OAuth Client ID
GOOGLE_AUTH_CLIENT_ID=xxx.apps.googleusercontent.com

# SMTP 寄件帳號（secret）
SMTP_SENDER_EMAIL=youremail@gmail.com
SMTP_SENDER_PASSWORD=應用程式密碼

# 檔案儲存策略（dev=local / prod=S3）
STORAGE_PROVIDER=local

# AWS / S3（僅 STORAGE_PROVIDER=S3 時必填）
AWS_ACCESS_KEY_ID=xxx
AWS_SECRET_ACCESS_KEY=xxx
AWS_REGION=ap-east-2
S3_BUCKET_NAME=xxx
S3_BUCKET_AVATAR_FOLDER=avatars
CLOUD_FRONT_URL=https://xxx.cloudfront.net
```

### 環境切換

| 變數 | Dev（本機 `dotnet run`） | Prod（`compose.prod.yml`） |
|------|------------------------|---------------------------|
| `ASPNETCORE_ENVIRONMENT` | `Development`（`launchSettings.json` 設定） | `Production`（`Dockerfile` 設定 + `compose.prod.yml` 顯式覆蓋） |
| `.env` 來源 | `DotNetEnv` 從 `../.env` 載入 | compose 的 `env_file: .env.production` 注入 container |
| `appsettings.json` | 一定載入 | 一定載入 |
| `appsettings.Development.json` | 載入（gitignored，目前內容為空） | 不載入 |

**根據 `app.Environment.IsDevelopment()` 切換的行為**：
- Swagger / OpenAPI / `UseHttpsRedirection`：僅 dev 啟用
- `db.Database.Migrate()` 自動執行：僅 prod 執行（dev 需手動 `dotnet ef database update`）
- `AuthCookieOptions.SameSite`：dev = `None`，其他環境 = `Strict`（白名單寬鬆原則）

## Architecture & Conventions

- **Layered architecture**：Controller → Service → DbContext（無 Repository 層）
- **DI lifetime**：所有 service 為 **Scoped**；`IConnectionMultiplexer` 與 `IAmazonS3` 為 **Singleton**
- **Namespace root**：`Bill_App_API`（API project）、`Bill_App_Cache`（Cache project）
- **Interface prefix**：`I`（IUserService、ICategoryService、ITokenService）
- **DTOs**：Request/Response 一律使用 C# `record` 類型
- **Models**：直接回傳 domain entity 或使用 response DTO（如 `UserProfileResponse`），**不使用 AutoMapper**
- **HttpContext Extension**：C# 14 extension member 語法，提供 `GetUserId()` / `HasUserId()` / `SetUserId()` 統一管理 `context.Items["userId"]`
- **Async pattern**：所有 I/O 必須 async（`Task<T>`）
- **EF Core**：Code-first，搭配明確的 migration
- **Nullable reference types**：專案層級啟用
- **Options pattern**：強型別設定，全部於 `Program.cs` 顯式賦值（不使用自動 binding）
- **Error handling**：`ApiException`（自訂 StatusCode + ResponseCode）由 `ExceptionHandlingMiddleware` 統一處理，401 時自動清除 cookies
- **Token Middleware Whitelist**：`TokenMiddlewareWhiteList` 靜態類別集中管理白名單路由
- **CORS**：允許前端 origin（`FRONT_END_URL` 環境變數），`AllowCredentials` 支援 cookie 跨域

## Authentication Flow

### Token 設計

| Token | 類型 | 預設效期 | 儲存位置 |
|-------|------|---------|---------|
| Access Token | JWT（HMAC SHA-256） | 15 分鐘（`JWT.DurationInMinutes`） | HttpOnly + Secure cookie |
| Refresh Token | GUID | 7 天（`RefreshToken.DurationInDays`） | HttpOnly + Secure cookie + Redis |

### Middleware Pipeline

```
Request
  → ExceptionHandlingMiddleware（最外層，統一例外處理）
  → LoginRateLimitMiddleware（IP + Email 雙重限流，僅攔截 login 路由）
  → AccessTokenMiddleware（JWT 驗證 + Redis UserSub cache）
  → RefreshTokenMiddleware（RT rotation + 15s 寬限期）
  → Controller
```

**ExceptionHandlingMiddleware**：
- `ApiException` → 用其 StatusCode + `ResponseWrap<object>.Error(message)`
- 其他 `Exception` → 500 + log + `ResponseWrap<object>.Error("伺服器內部錯誤")`
- 401 時自動清除 `accessToken` 與 `refreshToken` cookies

**LoginRateLimitMiddleware**：
- 僅攔截 `/api/user/login` 與 `/api/user/googleLogin`
- IP 限流（雙路由皆適用）：透過 `RemoteIpAddress` 取得 IP，Redis INCR 計數，超過上限回 429
- Email 限流（僅 `/api/user/login`）：透過 `EnableBuffering()` 讀 request body 取得 email，Redis INCR 計數
- Google 登入因 ID Token 無法暴力破解，僅做 IP 限流

**Whitelist 路由**（skip AccessToken & RefreshToken middlewares，定義於 `TokenMiddlewareWhiteList`）：
`/api/user/login`、`/api/user/signup`、`/api/user/logout`、`/api/user/verifyEmail`、`/api/user/googleLogin`、`/api/user/resendVerifyEmail`、`/api/user/forgetPassword`、`/api/user/resetPassword`

使用 `StartsWithSegments` 比對，支援動態路徑（如 `/api/user/verifyEmail/{token}`）。

**AccessTokenMiddleware**：
1. 讀取 `accessToken` cookie → 驗證 JWT（signature、issuer、audience、expiry）
2. 取出 `sub` claim → 查 Redis（`user:sub#{sub}`）取得 cache user info
3. Cache miss → fallback 到 DB，再寫入 Redis
4. 設定 `context.SetUserId(userId)` 供下游使用

**RefreshTokenMiddleware**（僅在 `context.HasUserId()` 為 false 時執行）：
1. 讀取 `refreshToken` cookie → 解析 GUID → 查 Redis（`auth:refreshToken#{guid}`）
2. `IsOld == Yes` → 直接通過（15s 寬限期，容忍併發請求）
3. `IsOld == No` → 執行 token rotation：
   - **先**從 ZSet 移除舊 RT（避免裝置數計算錯誤）
   - 產生新 access token + 新 refresh token
   - 將新 RT 存入 Redis（ZSet + Hash）
   - 將舊 RT 標記 `IsOld=Yes` 並設 15s TTL
   - 回應設定新 cookies

### Redis 資料結構

| Key Pattern | Type | 用途 |
|------------|------|------|
| `auth:refreshToken#{guid}` | Hash | RT 資料（UserId、Email、Sub、Name、Expire、IsOld）+ TTL |
| `auth:user#{userId}:refreshToken` | ZSet | 該使用者所有 RT，score = expiry timestamp(ms) |
| `user:sub#{sub}` | Hash | Cached user info（UserId、Email、Name）+ TTL |
| `email:verify#{token}` | String | Email 驗證 token → userId（GUID）+ TTL |
| `email:resendCooldown#{userId}` | String | 重送驗證信冷卻（TTL 60s） |
| `passwordReset#{token}` | String | 忘記密碼 token → userId（GUID）+ TTL |
| `email:forgetCooldown#{userId}` | String | 忘記密碼信冷卻（TTL 60s） |
| `rateLimit:login:ip#{ip}` | String | 登入 IP 限流計數（INCR + TTL） |
| `rateLimit:login:email#{email}` | String | 登入 Email 限流計數（INCR + TTL） |

### 多裝置支援

- 最大裝置數透過 `Device.MaxDevice` 設定（預設 5，於 `appsettings.json`）
- 登入 / 輪換時：清除 ZSet 中過期的 entry → 檢查數量 → 達到上限則踢出最舊
- `RotateRefreshToken()` 於 `UserService` 中處理裝置上限

### Signup Flow

1. 檢查 email 唯一性
2. 建立 User（`AuthProvider = Local`、`IsEmailVerified = false`、密碼以 BCrypt 雜湊）
3. 產生 GUID verification token → 存入 Redis（`email:verify#{token}` = userId，TTL 1 小時）
4. 透過 `EmailService` 寄送驗證信（MailKit SMTP），驗證連結指向**前端路由** `{FRONT_END_URL}/verifyEmail/{token}`
5. 同時 `Console.WriteLine` 記錄驗證連結（方便 dev 除錯）

### Email Verification Flow

1. `GET /api/user/verifyEmail/{token:guid}`（whitelist，免驗證）
2. 查 Redis `email:verify#{token}` → 取得 userId
3. 更新 DB `IsEmailVerified = true`
4. 刪除 Redis token（防止重用）

### Resend Verification Email Flow

1. `POST /api/user/resendVerifyEmail`（whitelist，免驗證）
2. Request body：`UserResendVerifyEmailRequest { Email }`
3. 查詢 DB → 若 user 不存在、AuthProvider 非 Local、或已驗證 → **靜默返回**（不洩漏帳號資訊）
4. 檢查 Redis `email:resendCooldown#{userId}` → 存在則靜默返回（60s 冷卻）
5. `Task.WhenAll` 並行設定冷卻 + 產生新 token 存入 Redis
6. 寄送驗證信
7. Controller 統一回 `Ok("若該信箱已註冊，驗證信已寄出")`

### Forget Password Flow

1. `POST /api/user/forgetPassword`（whitelist）
2. Request body：`UserForgetPasswordRequest { Email }`
3. 若 user 不存在、信箱未驗證、或 AuthProvider 為 Google → 靜默返回
4. 檢查 `email:forgetCooldown#{userId}` → 存在則靜默返回
5. `Task.WhenAll` 並行設定冷卻 + 產生 reset token 存入 Redis（`passwordReset#{token}`）
6. 寄送重設密碼信，連結指向 `{FRONT_END_URL}/revisePassword/{token}`
7. 統一回 `Ok("若該信箱已註冊，重設密碼信已寄出")`

### Reset Password Flow

1. `POST /api/user/resetPassword`（whitelist）
2. Request body：`UserResetPasswordRequest { Token, Password }`
3. 查 Redis `passwordReset#{token}` → 取得 userId，無效則拋 ApiException
4. BCrypt hash 新密碼 → 更新 user.Password
5. 讀取該用戶所有 RT（ZSet）→ 並行刪除所有 RT hash + reset token + RT ZSet（`Task.WhenAll`）
6. 強制所有裝置重新登入

### Change Password Flow（已登入）

1. `PUT /api/user/password`（需驗證）
2. Request body：`UserChangePasswordRequest { OldPassword, NewPassword }`
3. 從 `HttpContext.GetUserId()` 取得 userId → 查 DB
4. 驗證 `AuthProvider` 非 Google 且 `Password` 非 null → 否則拋「該帳號已綁定 Google」
5. BCrypt 驗證 OldPassword → 錯誤則拋「舊密碼錯誤」
6. BCrypt hash NewPassword → 更新 `Password` + `UpdatedAt`
7. 讀取所有 RT 並行刪除 + RT ZSet
8. Controller 清除 cookies（強制所有裝置重新登入）

### Login Flow（Local）

1. 驗證帳密（email + bcrypt）
2. `AuthProvider == Google` → 拋 ApiException（`ResponseCodeEnum.AccountBoundToGoogle`）
3. `IsEmailVerified == false` → 拋 ApiException（`ResponseCodeEnum.EmailNotVerified`）
4. 產生 access token + refresh token
5. 寫入 Redis（UserSub hash、RT ZSet、RT hash）
6. 設定兩個 HttpOnly cookies

### Google Login Flow

1. 前端傳 Google ID Token → `POST /api/user/googleLogin`
2. `GoogleJsonWebSignature.ValidateAsync()` 驗證（簽章 + audience）
3. 取出 email、name
4. 查 DB by email：
   - 存在 + `AuthProvider == Local` → 拒絕（`ResponseCodeEnum.AccountBoundToLocal`）
   - 存在 + `AuthProvider == Google` → 直接登入
   - 不存在 → 自動建立（`AuthProvider = Google`、`IsEmailVerified = true`、`Password = null`）
5. 後續同 Local login

### 帳號衝突規則

| 情境 | 結果 |
|------|------|
| Local login 但 AuthProvider == Google | 拒絕（`AccountBoundToGoogle`） |
| Google login 但 AuthProvider == Local | 拒絕（`AccountBoundToLocal`） |
| Signup 時 Email 已存在（任何 provider） | 拒絕 |

### Logout Flow

1. 從 cookie 讀 `refreshToken`（whitelist 跳過 middleware）
2. 從 ZSet 刪 RT（`DeleteUserRefreshTokenByMember`）
3. 刪 RT hash（`DeleteRefreshToken`）
4. 清除兩個 cookies

### Profile API

- `GET /api/user/profile`（需驗證）
- 透過 `HttpContext.GetUserId()` → 查 DB（Include Avatar）→ 回 `UserProfileResponse`（Name、Email、AuthProvider、IsEmailVerified、AvatarOriginalUrl、AvatarThumbUrl）

### Avatar API

- `POST /api/user/avatar`（需驗證，`multipart/form-data`）
- 支援 jpg / png / webp，上限 5MB
- 統一透過 ImageSharp 轉 WebP，產生兩張：
  - Original：400×400 → `{AvatarFolder}/{guid}_original.webp`
  - Thumbnail：100×100 → `{AvatarFolder}/{guid}_thumb.webp`
- 透過 `IFileStorageService` 抽象，由 `STORAGE_PROVIDER` 環境變數決定實作
- Avatar 為獨立 table，與 User 一對一關係
- 換頭像時刪除舊檔 + 舊 DB record，再新增新的
- DB 儲存 `OriginalUrl` / `ThumbUrl` 格式：
  - **Local**：相對路徑（`avatars/{guid}_original.webp`），由 `app.UseStaticFiles()` 提供，前端透過 `{API_BASE}/avatars/...` 存取
  - **S3**：完整 CloudFront URL，前端可直接 `<img src>` 使用

### Storage Provider 切換

- `STORAGE_PROVIDER` 環境變數控制（`local` / `S3`，預設 `Local`）
- `Program.cs` 讀取後決定 DI 註冊哪一個實作；`S3` 模式才會註冊 `AwsOptions` / `S3Options` / `IAmazonS3`，避免 Local 模式啟動時因缺少 AWS 環境變數而炸掉
- `IAmazonS3` 註冊為 **Singleton**（thread-safe + 內部 connection pool），透過 lambda 從 `AwsOptions` 顯式取憑證建構 `AmazonS3Client`
- AWS 設定步驟（IAM 權限、bucket policy、CloudFront OAC）詳見 `docs/S3Storage.md`

## Category API

- `POST /api/category` — 新增類別（Name、Type），驗證同 user 不可重複類別名稱
- `GET /api/category` — 取得該使用者所有未刪除的類別，回 `CategoryResponse`（Id、Name、TypeName、Type）
- `DELETE /api/category/{id:guid}` — 軟刪除（設定 `DeletedAt`）
- Category 帶 `TransactionTypeEnum Type`（Income=0 / Expense=1），TypeName 為中文對照

## Transaction API

- `POST /api/transaction` — 新增交易（CategoryId、Amount、Note），驗證 CategoryId 屬於該使用者
- `GET /api/transaction` — 查詢明細（分頁 + 篩選），支援 Type、CategoryId、StartDate、EndDate、Page、Limit
- `GET /api/transaction/typeList` — 取得交易類型下拉（`SelectListDto[]`）
- `PUT /api/transaction` — 修改（CategoryId 變更時驗證歸屬）
- `DELETE /api/transaction/{id:guid}` — 硬刪除
- Transaction **不**儲存 Type，透過 Navigation Property 從 Category 取得
- 查詢回傳 `PaginatedResponse<TransactionResponse>`（Data + Meta：Total、Page、Limit、TotalPages）
- 時間篩選由前端傳 UTC ISO 8601

## Statistics API

- `GET /api/statistics` — 收支統計摘要（需驗證）
- Query：`StartDate`、`EndDate`（required，UTC DateTime）
- 篩選：`>= StartDate AND < EndDate`
- 用 EF Core LINQ GroupBy（Category Type + Name）+ Navigation Property 自動 JOIN
- 回 `StatisticsResponse`：Income / Expense 兩組，各有 Total 和 Items（CategoryName、Amount、Percentage）
- Percentage：`Math.Round(amount / groupTotal * 100, 2)`

## Filters（Global）

於 `Program.cs` 透過 `AddControllers(options => options.Filters.Add<T>())` 註冊：

| Filter | Type | 用途 |
|--------|------|------|
| `LogActionFilter` | IActionFilter | 記錄 controller / action 名稱與參數 |
| `ResultWrapFilter` | IResultFilter | 將所有回應包成 `ResponseWrap<T>`（已包裝則略過） |

> 註：`GlobalExceptionFilter` 已移至 `ExceptionHandlingMiddleware`，統一處理 Middleware 與 Controller 層的例外。

### ResponseWrap\<T\>

統一回應 envelope（`Bill_App_API.Dtos.ResponseWrap<T>`）：

```csharp
{ Data: T?, Code: int, Message: string, Time: DateTime }
// 靜態工廠：ResponseWrap<T>.Success(data)、ResponseWrap<T>.Error(message)
```

### ResponseCodeEnum

定義於 `Bill_App_API.Enums.ResponseCodeEnum`，完整文件見 `docs/response-codes.md`。

| 範圍 | 模組 | 說明 |
|------|------|------|
| 0–999 | General | 通用（0=Success、1=Error） |
| 1001–1999 | Auth | 認證相關 |
| 2001–2999 | Category | 類別相關 |
| 3001–3999 | Transaction | 交易相關 |

目前已定義：

| Code | 常數 | 說明 |
|------|------|------|
| 1001 | `EmailNotVerified` | 信箱未驗證（前端顯示重送驗證信按鈕） |
| 1002 | `AccountBoundToGoogle` | 該帳號已綁定 Google |
| 1003 | `AccountBoundToLocal` | 該 Email 已使用密碼註冊 |

## Testing Conventions

單元測試位於 `Bill-App-Tests/Services/`，使用 xUnit + Moq + EF Core InMemoryDatabase。

### 測試類別結構

- xUnit 沒有 `[Setup]`，每個 `[Fact]` 都會 new 新 instance，所以「setup」寫在 constructor、「teardown」實作 `IDisposable`
- `BillDbContext` 與所有 `Mock<T>` 宣告為 `readonly` field，constructor 初始化，`_userService` 也在 constructor 組裝好
- `CreateDbContext()` 用 `Guid.NewGuid().ToString()` 當 InMemory database name，確保每個測試 DB 完全隔離
- `IOptions<T>` 統一透過 `Options.Create(new XxxOptions { ... })` 產生，沒用到的 Options 也要給空實例（constructor 要完整）

### Mock 原則

- **純函數不 Mock**（`PasswordHasher`、`BCrypt`）：判斷原則 — 碰 I/O（DB / 網路 / 檔案 / 寄信）→ Mock；純計算 / 純轉換 → 用真的
- **DbContext 不 Mock**：用 EF Core InMemoryDatabase 取代
- **Loose mock 預設行為**：Moq 對 `Task` / `Task<T>` 沒 Setup 也會自動回傳 `Task.CompletedTask` / `default`。**只有需要回傳特定值時才 Setup**
- **nullable 回傳值 Setup**：`_redisServiceMock.Setup(r => r.GetX(...)).ReturnsAsync((SomeType?)null)` — cast 到 nullable

### Assert 原則

- **驗證副作用，不只驗證回傳值**：例外分支要 assert「某些方法**沒被呼叫**」(`Times.Never`)，成功分支要 assert「某些方法**有被呼叫**」(`Times.Once` / `Times.Exactly(n)`)
- **例外測試檢查 `exception.Message` 和 `exception.Code`**：service 帶 `ResponseCodeEnum`（如 `AccountBoundToGoogle`、`EmailNotVerified`）時也要 `Assert.Equal(ResponseCodeEnum.Xxx, exception.Code)`
- **Verify 參數盡量用明確值，少用 `It.IsAny<T>()`**
- **密碼驗證用 `PasswordHasher.VerifyPassword(plain, hash)`**，比 `Assert.NotEqual(originalPassword, hashedPassword)` 更精確
- **EF InMemoryDatabase 讀回驗證時加 `AsNoTracking()`**：避免 change tracker 把記憶體中已修改但還沒 `SaveChangesAsync` 的 entity 當成 DB 狀態，造成假 pass

### 測試資料原則

- **塞 User 進 DB 時 `Password` 一律用 `PasswordHasher.HashPassword(...)`**：避免未來邏輯變動踩到 `BCrypt SaltParseException` 雷
- **non-null 欄位明確給值**：`AuthProvider`、`IsEmailVerified` 等即使有 enum default 也要明確寫出來

### 命名規範

- 格式：`MethodName_Scenario` 或 `MethodName_ScenarioExpectedResult`
- 範例：`Signup_EmailAlreadyExist`、`Login_AccountBoundGoogle`、`ChangePassword_Success`、`Logout_RefreshTokenTransformError`、`ResendVerifyEmail_UserStatusNotResend`

### 參數化測試

- 多個簡單值走同一條路徑用 `[Theory] + [InlineData]`
- 範例：`Logout_RefreshTokenTransformError` 用 `[InlineData("")] [InlineData("not-a-guid")] [InlineData("12345")]` 一次測三種非法 GUID 字串
- 需傳入複雜物件（如 `User` entity）改用 `[Theory] + [MemberData]`，搭配 `public static IEnumerable<object[]>` 屬性
- 範例：`ResendVerifyEmail_UserStatusNotResend` 用 `[MemberData(nameof(UserData))]` 傳入不同 AuthProvider / IsEmailVerified 組合的 User

### IFormFile Mock

- 使用 `FormFile(Stream.Null, 0, length, null, fileName)` 建構，搭配 `Headers = new HeaderDictionary()` 與 `ContentType = "image/jpeg"`
- 測試格式驗證傳入不合法 ContentType（如 `image/gif`）；測試大小驗證調整 `length`

### Service 內部方法依賴

- Service 自身的 public 方法（如 `UserService.RotateRefreshToken`）**不 mock**，讓它跟著執行，mock 的是它內部呼叫的介面（`IRedisService`、`ITokenService`）
- 需要 Setup 內部方法呼叫的底層服務（如 `_tokenServiceMock.Setup(t => t.GenerateRefreshToken()).Returns(mockGuid)`），讓整條呼叫鏈跑完

## Important Notes

- **更新 CLAUDE.md 後，務必檢查 `docs/` 資料夾**：確認 `docs/api-endpoints.md` 是否需要新增 / 修改 API 端點文件，以及 `docs/response-codes.md` 是否需要補上新的 ResponseCode。新功能涉及獨立流程（如 OAuth、Email、Logging）時，評估是否需要在 `docs/` 下新增說明文件。
- **Ignore `bin/` 和 `obj/` 資料夾**：掃描 / 搜尋 codebase 時排除
- **`.env` / `.env.production` 為 gitignored**：不要 commit secret，只 commit `.env.example`
- **CI pipeline**：`.github/workflows/ci.yml`，push/PR to main 或 dev 時執行 `dotnet restore` → `dotnet build` → `dotnet test`
- **Controllers 使用 `HttpContext.GetUserId()` extension 取得 userId**：不使用 `[Authorize]` attribute
- **Middleware 使用 `context.SetUserId()` / `context.HasUserId()`**：操作 userId
- **Cookie 設定統一透過 `AuthCookieOptions.Create()` 產生**：HttpOnly、Secure、SameSite 依環境切換
- **Token 商業邏輯位於 `UserService`**，JWT 加密邏輯位於 `TokenService`
- **`RedisController` (`GET /api/redis/redis-test`)** 為 Redis 連線測試端點

## Configuration Migration Status

| Status | 項目 |
|--------|------|
| ✅ | env 變數 → appsettings.json 搬遷完成（不變動 + 非機密的設定全部進 appsettings） |
| ✅ | `.env` / `.env.production` / `.env.example` 命名統一（`JWT_KEY` 單底線） |
| ✅ | `compose.prod.yml` 的 `ASPNETCORE_ENVIRONMENT` 設於正確服務（api） |
| ⏳ | Serilog 實際 wire up（套件已加入但 Program.cs 尚未呼叫 `UseSerilog`） |
| ⏳ | 雲端部署（目前 prod 仍為本機 docker compose 模擬） |
| 💭 | 可選：DB 改用 `ConnectionStrings:Default`（純標準慣例優化，不急） |

## 相關文件索引

- API 端點規格：[`docs/api-endpoints.md`](./docs/api-endpoints.md)
- ResponseCode 定義：[`docs/response-codes.md`](./docs/response-codes.md)
- Google OAuth：[`docs/GoogleAuth.md`](./docs/GoogleAuth.md)
- Gmail SMTP：[`docs/GmailSMTP.md`](./docs/GmailSMTP.md)
- 重送驗證信：[`docs/ResendAuthCode.md`](./docs/ResendAuthCode.md)
- AWS S3 + CloudFront：[`docs/S3Storage.md`](./docs/S3Storage.md)
- Docker 部署：[`docs/docker-cd-guide.md`](./docs/docker-cd-guide.md)
- Logging 策略：[`docs/Logging.md`](./docs/Logging.md)
