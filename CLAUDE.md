# Bill-App - Project Instructions

## Project Overview

Personal finance management API (記帳應用程式), built with ASP.NET Core 10 / C# 14.
Monorepo solution with two projects: `Bill-App-API` (Web API) and `Bill-App-Cache` (Redis class library).

## Tech Stack

- **Runtime**: .NET 10, C# 14
- **Database**: PostgreSQL 18.1 (via EF Core 10 + Npgsql)
- **Cache**: Redis 7.4 (via StackExchange.Redis)
- **Auth**: JWT (access token) + GUID (refresh token) + BCrypt password hashing + Email verification
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
├── Controllers/    # HTTP endpoints (UserController, CategoryController)
├── Services/       # Business logic (UserService, TokenService, CategoryService)
│   └── Interfaces/ # Service contracts (IUserService, ITokenService, ICategoryService)
├── Middlewares/     # AccessTokenMiddleware, RefreshTokenMiddleware
├── Filters/        # LogActionFilter, GlobalExceptionFilter, ResultWrapFilter
├── Exceptions/     # ApiException (custom exception with StatusCode)
├── Options/        # JwtOptions, MaxDeviceOptions, RefreshTokenOptions, UserCacheOptions, UserVerifyEmailOptions, AppOptions
├── Models/         # EF Core entities (User with AuthProvider, IsEmailVerified)
├── Dtos/           # Request/Response records + ResponseWrap<T>
├── Enums/          # TransactionTypeEnum, AuthProviderEnum (Local=0, Google=1)
├── Contexts/       # BillDbContext
├── Utils/          # PasswordHasher (BCrypt wrapper)
├── Migrations/     # EF Core migrations
└── Program.cs      # DI registration, filters & middleware pipeline

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
- **Models**: Return domain entities directly (no output DTOs / no AutoMapper)
- **Async pattern**: All I/O operations must be async (`Task<T>`)
- **EF Core**: Code-first approach with explicit migrations
- **Nullable reference types**: Enabled project-wide
- **Options pattern**: Strongly-typed config via `IOptions<T>` (JwtOptions, MaxDeviceOptions, RefreshTokenOptions, UserCacheOptions, UserVerifyEmailOptions, AppOptions)
- **Error handling**: `ApiException` for business errors (custom StatusCode), `GlobalExceptionFilter` distinguishes ApiException (4xx) from unexpected errors (500)

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

**Middleware 注入規則**：`IOptions<T>` 是 Singleton，放 constructor；Scoped 服務（ITokenService、IRedisService、IUserService）放 `InvokeAsync` 參數。

### Middleware Pipeline

```
Request → AccessTokenMiddleware → RefreshTokenMiddleware → Controller
```

**Whitelist routes** (skip both middlewares): `/api/user/login`, `/api/user/signup`, `/api/user/logout`, `/api/user/verifyEmail`

Whitelist 使用 `StartsWithSegments` 比對，支援動態路徑（如 `/api/user/verifyEmail/{token}`）。

**AccessTokenMiddleware**:
1. Read `accessToken` cookie → validate JWT (signature, issuer, audience, expiry)
2. Extract `sub` claim → query Redis (`user:sub#{sub}`) for cached user info
3. If cache miss → fallback to DB, then cache in Redis
4. Set `context.Items["userId"]` for downstream use

**RefreshTokenMiddleware** (only runs if `context.Items["userId"]` is NOT set):
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

### Multi-Device Support

- Max devices configurable via `MAX_DEVICE` env var (default: 5)
- On login/rotation: clean expired ZSet entries → check count → evict oldest if at limit
- `RotateRefreshToken()` in UserService handles device limit enforcement

### Signup Flow

1. Check email uniqueness
2. Create User (`AuthProvider = Local`, `IsEmailVerified = false`, password hashed with BCrypt)
3. Generate GUID verification token → store in Redis (`email:verify#{token}` = userId, TTL from config)
4. Log verification link to console (TODO: send email in production)

### Email Verification Flow

1. `GET /api/user/verifyEmail/{token:guid}` (whitelist route, no auth required)
2. Query Redis `email:verify#{token}` → get userId
3. Update DB `IsEmailVerified = true`
4. Delete Redis token (prevent reuse)

### Login Flow

1. Verify credentials (email + bcrypt password)
2. Check `IsEmailVerified` — reject if `false` (throw `ApiException`)
3. Generate access token (JWT) + refresh token (GUID)
4. Store in Redis: UserSub hash, RT ZSet entry, RT hash
5. Set HttpOnly cookies for both tokens

### Logout Flow

1. Read `refreshToken` from cookie (skip middleware via whitelist)
2. Delete RT from ZSet (`DeleteUserRefreshTokenByMember`)
3. Delete RT hash (`DeleteRefreshToken`)
4. Delete both cookies

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
```

## Important Notes

- **Ignore `bin/` and `obj/` folders** when scanning or searching the codebase
- `.env` files are gitignored - never commit secrets
- `.github/workflows/` exists but has no CI/CD pipelines yet
- Controllers use `context.Items["userId"]` for auth (not `[Authorize]` attribute)
- Token-related business logic lives in `UserService`, JWT cryptography in `TokenService`

## Filters (Global)

Registered in `Program.cs` via `AddControllers(options => options.Filters.Add<T>())`:

| Filter | Type | Purpose |
|--------|------|---------|
| `LogActionFilter` | IActionFilter | Logs controller/action name and arguments |
| `GlobalExceptionFilter` | IExceptionFilter | `ApiException` → 用其 StatusCode；其他 Exception → 500 + log |
| `ResultWrapFilter` | IResultFilter | Wraps all responses in `ResponseWrap<T>` (skips if already wrapped) |

### ResponseWrap\<T\>

Unified response envelope (`Bill_App_API.Dtos.ResponseWrap<T>`):

```csharp
{ Data: T?, Code: int, Message: string, Time: DateTime }
// Code 0 = success, Code 1 = error
// Static factories: ResponseWrap<T>.Success(data), ResponseWrap<T>.Error(message)
```
