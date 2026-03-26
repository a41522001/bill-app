# Bill-App (記帳應用程式) - 專案概覽

這是一個基於 **ASP.NET Core 10.0** 開發的後端 API 專案，旨在提供帳務管理、使用者驗證及分類管理功能。

## 專案技術棧
- **框架**: ASP.NET Core 10.0 (Web API)
- **語言**: C# 14 / .NET 10
- **資料庫**: PostgreSQL (透過 Entity Framework Core 10.0)
- **快取**: Redis (預留於 docker-compose.yml)
- **驗證**: JWT (JSON Web Token) + BCrypt 密碼雜湊
- **容器化**: Docker & Docker Compose

## 目錄結構說明
- `Bill-App-API/`: 核心 API 程式碼。
  - `Contexts/`: 資料庫上下文 (EF Core `BillDbContext`)。
  - `Controllers/`: API 端點實作 (User, Category)。
  - `Dtos/`: 資料傳輸物件 (Request/Response 模型)。
  - `Models/`: 領域模型 (User, Category, Transaction)。
  - `Services/`: 業務邏輯層 (UserService, JwtService, CategoryService)。
  - `Utils/`: 工具類 (如 `PasswordHasher`)。
- `Migrations/`: 資料庫遷移記錄。

## 建置與執行指令
### 環境準備
1. 確保已安裝 .NET 10 SDK。
2. 建立 `.env` 檔案並設定資料庫資訊 (參考 `docker-compose.yml`)。

### 啟動服務 (Docker)
```bash
# 啟動 PostgreSQL 與 Redis
docker-compose up -d
```

### 執行專案
```bash
cd Bill-App-API
dotnet run
```

### 資料庫遷移
```bash
# 新增遷移
dotnet ef migrations add <MigrationName> --project Bill-App-API

# 更新資料庫
dotnet ef database update --project Bill-App-API
```

## 開發慣例與規範
- **非同步開發**: 所有 I/O 密集型操作 (資料庫、API 調用) 必須使用 `async/await`。
- **依賴注入**: 服務應透過介面 (`Interfaces/`) 注入，並在 `Program.cs` 中註冊。
- **安全性**: 
  - 密碼必須透過 `PasswordHasher.HashPassword` 加密後儲存。
  - 需要驗證的 API 應使用 `[Authorize]` 標籤。
- **命名空間**: 統一使用 `Bill_App` 作為根命名空間。
- **實體關聯**: 
  - `Transaction` 關聯至 `User` (多對一) 與 `Category` (多對一)。
  - `User` 實體包含 `Sub` (用於外部標識) 與 `Id` (內部主鍵)。

## TODO / 待辦事項
- [ ] 完整實作 `TransactionController` 以處理帳務明細。
- [ ] 整合 Redis 用於 Token 黑名單或快取。
- [ ] 撰寫單元測試 (目前的目錄中尚未看到測試專案)。
- [ ] 完善全域異常處理 (Global Exception Handling)。
