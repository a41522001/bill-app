# Bill-App 個人記帳 API

> 以 ASP.NET Core 10 / C# 14 打造的個人財務管理 API，整合 JWT + Refresh Token 雙令牌、Google OAuth、Email 驗證、Redis 快取與多裝置登入管理。

[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18.1-336791)](https://www.postgresql.org/)
[![Redis](https://img.shields.io/badge/Redis-7.4-DC382D)](https://redis.io/)
[![CI](https://github.com/<owner>/<repo>/actions/workflows/ci.yml/badge.svg)](./.github/workflows/ci.yml)

---

## ✨ 主要功能

### 帳號與認證
- 一般註冊 / 登入（BCrypt 密碼雜湊）
- Google OAuth 登入（ID Token 驗證）
- Email 驗證信機制（含 60s 重送冷卻）
- 忘記密碼 / 重設密碼（Email 連結）
- 已登入狀態下變更密碼
- JWT (Access Token) + GUID (Refresh Token) 雙令牌設計
- **多裝置登入管理**（預設 5 台，超過自動踢最舊裝置）
- **Refresh Token Rotation** + 15 秒舊 Token 寬限期（併發容錯）
- **登入限流**(依 IP + 依 Email 雙重限制)
- HttpOnly + Secure Cookie 儲存 Token

### 記帳核心
- 類別管理（收入 / 支出，軟刪除）
- 交易紀錄 CRUD（分頁 + 篩選：類型 / 類別 / 日期區間）
- 收支統計摘要（類別占比、Income / Expense 分組）

### 個人化
- 頭像上傳（jpg / png / webp，自動轉 WebP，產生 400x400 與 100x100 兩張）
- 檔案儲存抽象化（`IFileStorageService`，可切換 Local ↔ S3）

---

## 🛠 技術棧

| 類別 | 技術 |
|------|------|
| Runtime | .NET 10, C# 14 |
| Web Framework | ASP.NET Core 10 |
| Database | PostgreSQL 18.1 + EF Core 10 (Npgsql) |
| Cache | Redis 7.4 (StackExchange.Redis) |
| Auth | JWT + GUID Refresh Token + BCrypt + Google.Apis.Auth |
| Email | MailKit (SMTP) |
| Image | SixLabors.ImageSharp |
| Testing | xUnit + Moq + EF Core InMemory |
| Infra | Docker Compose |
| CI | GitHub Actions |

---

## 🏗 系統架構

```
Client (Cookie: accessToken + refreshToken)
        │
        ▼
┌─────────────────────────────────────────────┐
│  ExceptionHandlingMiddleware (統一例外處理) │
│  ↓                                          │
│  LoginRateLimitMiddleware (IP + Email 限流) │
│  ↓                                          │
│  AccessTokenMiddleware (JWT 驗證 + 快取)    │
│  ↓                                          │
│  RefreshTokenMiddleware (RT Rotation)       │
│  ↓                                          │
│  Controllers → Services → DbContext         │
└─────────────────────────────────────────────┘
        │                     │
        ▼                     ▼
   PostgreSQL            Redis
   (Users / Categories   (RT / UserSub Cache /
    Transactions /        Email Tokens /
    Avatars)              Rate Limit Counters)
```

**分層**：Controller → Service → DbContext（無 Repository 層）
**回應包裝**：所有 API 透過 `ResultWrapFilter` 統一包成 `ResponseWrap<T>`

---

## 📁 專案結構

```
Bill-App.sln
├── Bill-App-API/         # Web API (主專案)
│   ├── Controllers/      # User / Category / Transaction / Statistics / Redis
│   ├── Services/         # 商業邏輯 + Interfaces/
│   ├── Middlewares/      # ExceptionHandling / RateLimit / AccessToken / RefreshToken
│   ├── Filters/          # LogActionFilter / ResultWrapFilter
│   ├── Models/           # User / Category / Transaction / Avatar
│   ├── Dtos/             # Request / Response records
│   ├── Options/          # 強型別設定（IOptions<T>）
│   ├── Enums/            # TransactionType / AuthProvider / ResponseCode
│   ├── Exceptions/       # ApiException
│   ├── Extensions/       # HttpContextExtension (GetUserId/SetUserId)
│   ├── Utils/            # PasswordHasher (BCrypt)
│   ├── Migrations/       # EF Core migrations
│   └── Program.cs
├── Bill-App-Cache/       # Redis Class Library
│   ├── IRedisService.cs
│   ├── RedisService.cs
│   ├── RedisDto.cs
│   └── RedisKey.cs
├── Bill-App-Tests/       # xUnit + Moq + EF InMemory
│   └── Services/
├── docs/                 # API / ResponseCode / OAuth / SMTP 文件
├── docker-compose.yml    # Postgres + Redis
└── .github/workflows/    # CI pipeline
```

---

## 🚀 快速開始

### 前置需求
- .NET 10 SDK
- Docker Desktop
- (可選) Postman / Swagger UI

### 步驟

```bash
# 1. clone 專案
git clone <repo-url> && cd bill-app

# 2. 建立 .env（參考下方環境變數章節）
cp .env.example .env

# 3. 啟動 Postgres + Redis
docker compose up -d

# 4. 套用資料庫 migration
dotnet ef database update --project Bill-App-API

# 5. 啟動 API
dotnet run --project Bill-App-API
# 或熱重載
dotnet watch run --project Bill-App-API
```

- API: `http://localhost:5148` / `https://localhost:7188`
- Swagger: `https://localhost:7188/swagger`（僅 Development）

---

## 🔐 環境變數

完整列表見 `CLAUDE.md`，以下為必填項：

```env
# Database
DB_USER=postgres
DB_PASSWORD=your_password
DB_NAME=billapp

# JWT
JWT__KEY=至少32字元的隨機字串
JWT__ISSUER=bill-app
JWT__AUDIENCE=bill-app-client
JWT__DURATION_IN_MINUTES=15

# Refresh Token
REFRESH_TOKEN__DURATION_IN_DAY=7
REFRESH_TOKEN__OLD_TOKEN_GRACE_IN_SECONDS=15

# 多裝置 / 快取
MAX_DEVICE=5
USER_CACHE__TTL_IN_HOURS=24
USER_VERIFY_EMAIL__TTL_IN_HOURS=1

# Google OAuth
GOOGLE_AUTH_CLIENT_ID=xxx.apps.googleusercontent.com

# SMTP（Email 驗證 / 重設密碼）
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_SENDER_EMAIL=your@gmail.com
SMTP_SENDER_NAME=Bill-App
SMTP_SENDER_PASSWORD=應用程式密碼

# Frontend
FRONT_END_URL=http://localhost:5173

# 登入限流
LOGIN_RATE_LIMIT_BY_IP_COUNT=20
LOGIN_RATE_LIMIT_BY_EMAIL_COUNT=5
LOGIN_RATE_LIMIT_TTL_MINUTE=15
```

---

## 📡 API 概覽

完整端點規格請見 [`docs/api-endpoints.md`](./docs/api-endpoints.md)。

| 模組 | Endpoint | 說明 |
|------|----------|------|
| **User** | `POST /api/user/signup` | 註冊（寄驗證信） |
| | `GET /api/user/verifyEmail/{token}` | 驗證 Email |
| | `POST /api/user/resendVerifyEmail` | 重送驗證信 |
| | `POST /api/user/login` | 一般登入 |
| | `POST /api/user/googleLogin` | Google 登入 |
| | `POST /api/user/logout` | 登出 |
| | `POST /api/user/forgetPassword` | 忘記密碼 |
| | `POST /api/user/resetPassword` | 重設密碼 |
| | `PUT /api/user/password` | 變更密碼（已登入） |
| | `GET /api/user/profile` | 取得個人資料 |
| | `POST /api/user/avatar` | 上傳頭像 |
| **Category** | `POST/GET/DELETE /api/category` | 類別 CRUD |
| **Transaction** | `POST/GET/PUT/DELETE /api/transaction` | 交易 CRUD（含分頁） |
| | `GET /api/transaction/typeList` | 類型下拉選單 |
| **Statistics** | `GET /api/statistics` | 收支統計摘要 |

所有回應統一格式：

```json
{ "data": "<T>", "code": 0, "message": "OK", "time": "..." }
```

---

## 🔒 認證機制重點

### Token 設計

| Token | 類型 | 預設效期 | 儲存位置 |
|-------|------|---------|----------|
| Access Token | JWT (HS256) | 15 分鐘 | HttpOnly Cookie |
| Refresh Token | GUID | 7 天 | HttpOnly Cookie + Redis |

### Refresh Token Rotation
1. 每次刷新會 **產生新 RT**，舊 RT 標記 `IsOld=Yes` 並保留 15 秒寬限期（解決併發請求）
2. 用 Redis ZSet 管理使用者所有裝置的 RT，超過 `MAX_DEVICE` 自動踢最舊
3. 變更密碼 / 重設密碼會 **強制清空所有裝置 RT**

### 帳號衝突規則

| 情境 | 結果 |
|------|------|
| 用密碼登入但帳號是 Google 註冊 | 拒絕（請用 Google 登入） |
| 用 Google 登入但 Email 已用密碼註冊 | 拒絕（請用密碼登入） |
| 註冊時 Email 已存在（任何 provider） | 拒絕 |

---

## 🧪 測試

```bash
dotnet test Bill-App-Tests
```

- **xUnit + Moq + EF Core InMemoryDatabase**
- 已涵蓋：`UserService` / `CategoryService` / `TransactionService`
- 規範：純函數不 Mock（如 `PasswordHasher`），DbContext 用 InMemory，例外測試需驗證 `Message` + `ResponseCode`

---

## 🔄 CI/CD

`.github/workflows/ci.yml`：push / PR 至 `main` 時自動執行

```
dotnet restore → dotnet build → dotnet test
```
