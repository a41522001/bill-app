# Bill-App 個人記帳 API

> 以 ASP.NET Core 10 / C# 14 打造的個人財務管理 API，整合 JWT + Refresh Token 雙令牌、Google OAuth、Email 驗證、Redis 快取、多裝置登入管理，以及可切換的本機 / S3 檔案儲存。

[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120)](https://learn.microsoft.com/dotnet/csharp/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18.1-336791)](https://www.postgresql.org/)
[![Redis](https://img.shields.io/badge/Redis-7.4-DC382D)](https://redis.io/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED)](https://docs.docker.com/compose/)
[![CI](https://github.com/a41522001/bill-app/actions/workflows/ci.yml/badge.svg)](./.github/workflows/ci.yml)

---

## ✨ 主要功能

### 帳號與認證
- 一般註冊 / 登入（BCrypt 密碼雜湊）
- Google OAuth 登入（ID Token 驗證，自動建立帳號）
- Email 驗證信機制（含 60 秒重送冷卻）
- 忘記密碼 / 重設密碼（Email 連結）
- 已登入狀態下變更密碼
- JWT（Access Token）+ GUID（Refresh Token）雙令牌
- **多裝置登入管理**（預設 5 台，超過自動踢最舊）
- **Refresh Token Rotation** + 15 秒寬限期（併發容錯）
- **登入限流**（IP + Email 雙重限制）
- HttpOnly + Secure Cookie 儲存 Token

### 記帳核心
- 類別管理（收入 / 支出，軟刪除）
- 交易紀錄 CRUD（分頁 + 篩選：類型 / 類別 / 日期區間）
- 收支統計摘要（類別占比，Income / Expense 分組）

### 個人化
- 頭像上傳（jpg / png / webp，自動轉 WebP，產生 400×400 與 100×100 兩張）
- 檔案儲存抽象化（`IFileStorageService`，可切換 Local ↔ S3 + CloudFront）

---

## 🛠 技術棧

| 類別 | 技術 |
|------|------|
| Runtime | .NET 10 / C# 14 |
| Web Framework | ASP.NET Core 10 |
| Database | PostgreSQL 18.1 + EF Core 10（Npgsql） |
| Cache | Redis 7.4（StackExchange.Redis） |
| Auth | JWT + GUID Refresh Token + BCrypt + Google.Apis.Auth |
| Email | MailKit（SMTP） |
| Image | SixLabors.ImageSharp |
| File Storage | 本機磁碟 / AWS S3 + CloudFront（透過 `IFileStorageService` 抽象） |
| Logging | Serilog（已加入套件，配置策略見 `docs/Logging.md`） |
| Testing | xUnit + Moq + EF Core InMemoryDatabase |
| Infra | Docker Compose（dev / prod 各一份） |
| CI | GitHub Actions（restore → build → test） |

---

## 🏗 系統架構

```
Client（Cookie：accessToken + refreshToken）
        │
        ▼
┌──────────────────────────────────────────────┐
│  ExceptionHandlingMiddleware（統一例外處理） │
│   ↓                                          │
│  LoginRateLimitMiddleware（IP + Email 限流） │
│   ↓                                          │
│  AccessTokenMiddleware（JWT 驗證 + 快取）    │
│   ↓                                          │
│  RefreshTokenMiddleware（RT Rotation）       │
│   ↓                                          │
│  Controllers → Services → DbContext          │
└──────────────────────────────────────────────┘
        │                       │
        ▼                       ▼
   PostgreSQL                Redis
  (Users / Categories /    (RT Hash & ZSet /
   Transactions /           UserSub Cache /
   Avatars)                 Email Tokens /
                            Rate Limit Counters)
```

**分層**：Controller → Service → DbContext（無 Repository 層）
**回應包裝**：所有 API 透過 `ResultWrapFilter` 統一包成 `ResponseWrap<T>`
**錯誤處理**：`ApiException` + `ExceptionHandlingMiddleware` 統一處理（含自動清除過期 cookies）

---

## 📁 專案結構

```
Bill-App.sln
├── Bill-App-API/              # Web API 主專案
│   ├── Controllers/           # User / Category / Transaction / Statistics / Redis
│   ├── Services/              # 商業邏輯 + Interfaces/
│   ├── Middlewares/           # ExceptionHandling / RateLimit / AccessToken / RefreshToken
│   ├── Filters/               # LogActionFilter / ResultWrapFilter
│   ├── Models/                # User / Category / Transaction / Avatar
│   ├── Dtos/                  # Request / Response records
│   ├── Options/               # 強型別設定（IOptions<T>）
│   ├── Enums/                 # TransactionType / AuthProvider / ResponseCode
│   ├── Exceptions/            # ApiException
│   ├── Extensions/            # HttpContextExtension（GetUserId / SetUserId）
│   ├── Utils/                 # PasswordHasher（BCrypt）
│   ├── Migrations/            # EF Core migrations（6 個）
│   ├── appsettings.json       # 不變動 + 非機密預設值
│   └── Program.cs
├── Bill-App-Cache/            # Redis Class Library
│   ├── IRedisService.cs       # Redis 操作介面（28 個方法）
│   ├── RedisService.cs
│   ├── RedisDto.cs            # UserSubHash / RefreshTokenHash records
│   └── RedisKey.cs            # 9 個 key pattern
├── Bill-App-Tests/            # xUnit + Moq + EF InMemory
│   └── Services/              # UserService / CategoryService / TransactionService 測試
├── docs/                      # API / ResponseCode / OAuth / SMTP / S3 / Docker / Logging
├── compose.yml                # Dev：Postgres + Redis
├── compose.prod.yml           # Prod：Postgres + Redis + API
├── Dockerfile                 # Multi-stage build
└── .github/workflows/ci.yml   # CI pipeline
```

---

## 🚀 快速開始

### 前置需求
- .NET 10 SDK
- Docker Desktop
- （可選）Postman / Swagger UI

### Dev 模式（本機 `dotnet run` + 容器跑依賴）

```bash
# 1. clone 專案
git clone <repo-url> && cd bill-app

# 2. 建立 .env（從範本複製，再填入機密）
cp .env.example .env

# 3. 啟動 Postgres + Redis
docker compose up -d

# 4. 套用資料庫 migration（首次或新增 migration 後）
dotnet ef database update --project Bill-App-API

# 5. 啟動 API
dotnet run --project Bill-App-API
# 或熱重載
dotnet watch run --project Bill-App-API
```

- API：`http://localhost:5148` / `https://localhost:7188`
- Swagger：`https://localhost:7188/swagger`（僅 Development）
- Health：`GET /health`

### Prod 模式（整套用容器跑，目前仍為本機模擬）

```bash
# 1. 建立 .env.production（從 .env.example 複製，填入 prod 機密）
cp .env.example .env.production

# 2. 啟動整套（首次需要 --build）
docker compose -f compose.prod.yml up -d --build
```

- API（容器）：`http://localhost:8080`
- Migration 由 `Program.cs` 啟動時自動執行（`db.Database.Migrate()`，僅在非 Development 環境）

---

## 🔐 設定策略

**核心原則**：
- `appsettings.json` 放「**不變動 + 非機密**」的設定預設值
- `.env` / `.env.production` 只放「**機密 + 跨環境變動**」的值
- Dev 與 Prod **共用同一份 `appsettings.json`**

### `appsettings.json`（commit 進 repo）

```jsonc
{
  "JWT": {
    "Issuer": "bill-app",
    "Audience": "bill-app",
    "DurationInMinutes": 15
  },
  "RefreshToken": {
    "DurationInDays": 7,
    "OldTokenGraceInSeconds": 15
  },
  "Device": { "MaxDevice": 5 },
  "SMTP": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "SenderName": "Bill App"
  },
  "LoginRateLimit": {
    "IP": 20,
    "Email": 5,
    "TtlMinutes": 15
  },
  "UserCache": { "TtlInHours": 24 },
  "UserVerifyEmailCache": { "TtlInHours": 1 }
}
```

### `.env`（gitignored，僅機密 + 跨環境變動）

```env
# Database
DB_USER=admin
DB_PASSWORD=your_password
DB_NAME=bill-app
DB_HOST=localhost      # prod 由 compose.prod.yml 覆蓋成 db
DB_PORT=5432

# Redis
REDIS_CONNECTION=localhost:6379  # prod 由 compose.prod.yml 覆蓋成 redis:6379

# JWT 簽章
JWT_KEY=至少32字元的隨機字串

# 應用程式 URL
APP_DOMAIN=https://localhost:7188
FRONT_END_URL=http://localhost:5173

# Google OAuth
GOOGLE_AUTH_CLIENT_ID=xxx.apps.googleusercontent.com

# SMTP 寄件帳號
SMTP_SENDER_EMAIL=your@gmail.com
SMTP_SENDER_PASSWORD=Gmail 應用程式密碼

# 檔案儲存（dev=local / prod=S3）
STORAGE_PROVIDER=local

# AWS / S3（僅 STORAGE_PROVIDER=S3 時必填）
AWS_ACCESS_KEY_ID=xxx
AWS_SECRET_ACCESS_KEY=xxx
AWS_REGION=ap-east-2
S3_BUCKET_NAME=xxx
S3_BUCKET_AVATAR_FOLDER=avatars
CLOUD_FRONT_URL=https://xxx.cloudfront.net
```

> S3 模式設定詳情見 [`docs/S3Storage.md`](./docs/S3Storage.md)

---

## 📡 API 概覽

完整端點規格見 [`docs/api-endpoints.md`](./docs/api-endpoints.md)。

| 模組 | Endpoint | 說明 |
|------|----------|------|
| **User** | `POST /api/user/signup` | 註冊（寄驗證信） |
| | `GET /api/user/verifyEmail/{token}` | 驗證 Email |
| | `POST /api/user/resendVerifyEmail` | 重送驗證信（60s 冷卻） |
| | `POST /api/user/login` | 一般登入 |
| | `POST /api/user/googleLogin` | Google 登入 |
| | `POST /api/user/logout` | 登出 |
| | `POST /api/user/forgetPassword` | 忘記密碼（寄信） |
| | `POST /api/user/resetPassword` | 重設密碼（憑 token） |
| | `PUT /api/user/password` | 變更密碼（已登入） |
| | `GET /api/user/profile` | 取得個人資料 |
| | `POST /api/user/avatar` | 上傳頭像 |
| **Category** | `POST/GET /api/category` | 類別新增 / 列表 |
| | `DELETE /api/category/{id}` | 類別軟刪除 |
| **Transaction** | `POST/GET/PUT /api/transaction` | 交易 CRUD（含分頁 + 篩選） |
| | `DELETE /api/transaction/{id}` | 交易硬刪除 |
| | `GET /api/transaction/typeList` | 類型下拉選單 |
| **Statistics** | `GET /api/statistics` | 收支統計摘要 |
| **Misc** | `GET /health` | 健康檢查 |
| | `GET /api/redis/redis-test` | Redis 連線測試 |

所有回應統一格式：

```json
{ "data": "<T>", "code": 0, "message": "OK", "time": "2026-05-10T10:00:00Z" }
```

---

## 🔒 認證機制重點

### Token 設計

| Token | 類型 | 預設效期 | 儲存位置 |
|-------|------|---------|---------|
| Access Token | JWT（HS256） | 15 分鐘 | HttpOnly Cookie |
| Refresh Token | GUID | 7 天 | HttpOnly Cookie + Redis |

### Refresh Token Rotation

1. 每次刷新會 **產生新 RT**，舊 RT 標記 `IsOld=Yes` 並保留 15 秒寬限期（解決併發請求）
2. 用 Redis ZSet 管理使用者所有裝置的 RT，超過 `MaxDevice` 自動踢最舊
3. 變更密碼 / 重設密碼會 **強制清空所有裝置的 RT**

### 帳號衝突規則

| 情境 | 結果 |
|------|------|
| 用密碼登入但帳號是 Google 註冊 | 拒絕（請用 Google 登入） |
| 用 Google 登入但 Email 已用密碼註冊 | 拒絕（請用密碼登入） |
| 註冊時 Email 已存在（任何 provider） | 拒絕 |

### Cookie 設定

- **HttpOnly + Secure**：避免 XSS 偷 token
- **SameSite**：Dev = `None`（方便跨 origin 開發），其他環境 = `Strict`（白名單寬鬆原則，未知環境預設嚴格）

---

## 🧪 測試

```bash
dotnet test Bill-App-Tests
```

- **xUnit + Moq + EF Core InMemoryDatabase**
- 已涵蓋：`UserService`（34 個 case）/ `CategoryService`（4 個）/ `TransactionService`（10 個）
- 規範：純函數不 Mock（如 `PasswordHasher`），DbContext 用 InMemory，例外測試需驗證 `Message` + `ResponseCode`
- 詳細測試規範見 [`CLAUDE.md`](./CLAUDE.md#testing-conventions)

---

## 🔄 CI/CD

`.github/workflows/ci.yml`：push / PR 至 `main` 或 `dev` 分支時自動執行

```
dotnet restore → dotnet build (--no-restore) → dotnet test (--no-build)
```

> 部署管線（GitHub Actions → ghcr.io → VPS）規劃見 [`docs/docker-cd-guide.md`](./docs/docker-cd-guide.md)

---

## 📚 文件索引

| 文件 | 內容 |
|------|------|
| [CLAUDE.md](./CLAUDE.md) | 專案規範與架構細節（給維護者 / AI） |
| [docs/api-endpoints.md](./docs/api-endpoints.md) | API 端點完整規格 |
| [docs/response-codes.md](./docs/response-codes.md) | ResponseCodeEnum 定義 |
| [docs/GoogleAuth.md](./docs/GoogleAuth.md) | Google OAuth 整合 |
| [docs/GmailSMTP.md](./docs/GmailSMTP.md) | Gmail SMTP 設定 |
| [docs/ResendAuthCode.md](./docs/ResendAuthCode.md) | 重送驗證信流程 |
| [docs/S3Storage.md](./docs/S3Storage.md) | AWS S3 + CloudFront 設定 |
| [docs/docker-cd-guide.md](./docs/docker-cd-guide.md) | Docker 部署指南 |
| [docs/Logging.md](./docs/Logging.md) | Serilog 策略 |

---

## 📝 License

MIT
