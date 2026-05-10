# Logging 策略

本文件定義 Bill-App 專案的日誌（log）策略，包含技術選型、檔案結構、log level 規範、結構化欄位設計，以及實作要點。

---

## 技術選型：Serilog

| 項目 | 選擇 | 理由 |
|------|------|------|
| Logging framework | **Serilog** | 結構化 log 的事實標準、與 ASP.NET Core 整合佳、sink 生態完整 |
| Console sink | `Serilog.Sinks.Console` | dev 環境人類可讀格式 |
| File sink | `Serilog.Sinks.File` | 寫入本地檔案 |
| 動態路徑分流 | `Serilog.Sinks.Map` | 根據日期動態決定檔案路徑（年/月/日階層） |
| Enricher | `Serilog.Enrichers.Environment`、`Serilog.Enrichers.Thread` | 加上 MachineName、ThreadId 等通用欄位 |

> **不採用 NLog / log4net**：兩者都是字串導向 logger，不像 Serilog 原生支援結構化欄位。
> **不採用 Microsoft.Extensions.Logging 內建 Console**：缺檔案輸出、缺結構化、缺 enricher。

---

## 檔案結構

採用 **年 / 月 / 日階層**，每天兩個檔（JSON + 純文字），方便不同情境使用：

```
logs/
├── 2026/
│   ├── 05/
│   │   ├── 05.json          ← 結構化 log（給機器讀 / 未來接 ELK / Seq）
│   │   ├── 05.txt           ← 人類可讀 log（dev 直接 tail，肉眼除錯用）
│   │   ├── 06.json
│   │   └── 06.txt
│   └── 06/
└── 2025/
    └── 12/
        └── 31.json
```

### 設計理由

- **年/月階層**：方便歸檔與刪舊（直接 `rm -rf logs/2025/`）
- **JSON + TXT 雙格式**：
  - `.json`：每行一個 JSON object（NDJSON），未來可直接灌進 ELK、Seq、CloudWatch
  - `.txt`：人類可讀格式（`{Timestamp} [{Level}] {Message}`），dev 環境 `tail -f` 看流
- **不按 controller / service 分檔**：跨層追問題時需要 timeline 對齊，分檔反而傷害可查性。要篩 controller 用結構化欄位 `SourceContext` 即可

### 不採用的方案（與理由）

| 方案 | 不採用理由 |
|------|----------|
| 平面檔案 `logs/log-20260505.json` | 檔案多時難看、不好歸檔 |
| 按 controller 分檔 | 跨層追問題痛苦，正確做法是用 `SourceContext` 欄位篩 |
| 按 log level 分檔（error.log / info.log） | 同一個 request 的 log 會散在不同檔，timeline 對不上 |
| 單一巨大檔案 | 檔案肥大、難 rotate、刪舊麻煩 |

---

## Serilog 路徑實作要點

Serilog 內建的 `RollingInterval.Day` 會產生**平面檔案**（`log-20260505.txt`），**不支援巢狀資料夾**。

要做到 `logs/2026/05/05.json`，使用 `Serilog.Sinks.Map` 配合自訂 enricher：

### 概念

1. 寫一個 enricher，在每筆 log 推入 `LogDate` property，值為 `yyyy/MM/dd` 格式（例如 `2026/05/05`）
2. `WriteTo.Map(keyPropertyName: "LogDate", ...)` 根據這個 property 動態決定寫入哪個檔
3. Map 的 sink configurator 用 key 拼出完整路徑：`logs/{key}.json`

### appsettings.json 設定範例

> 此處示意配置結構，實作時依 Serilog.Sinks.Map 文件調整。

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId", "WithLogDate" ],
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "Map",
        "Args": {
          "keyPropertyName": "LogDate",
          "defaultKey": "unknown",
          "configure": [
            {
              "Name": "File",
              "Args": {
                "path": "logs/{LogDate}.json",
                "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact",
                "rollOnFileSizeLimit": true,
                "fileSizeLimitBytes": 104857600,
                "shared": true
              }
            },
            {
              "Name": "File",
              "Args": {
                "path": "logs/{LogDate}.txt",
                "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {SourceContext} - {Message:lj}{NewLine}{Exception}",
                "rollOnFileSizeLimit": true,
                "fileSizeLimitBytes": 104857600,
                "shared": true
              }
            }
          ]
        }
      }
    ]
  }
}
```

### LogDateEnricher 實作概念

```csharp
public class LogDateEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var logDate = DateTime.UtcNow.ToString("yyyy/MM/dd");
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LogDate", logDate));
    }
}
```

註冊到 LoggerConfiguration：`.Enrich.With<LogDateEnricher>()`。

> **注意**：跨日時 Map 會自動切換到新檔（因為 LogDate 變了），這正是我們要的行為。

---

## Log Level 規範

| Level | 使用時機 | 範例 |
|-------|---------|------|
| `Trace` | 不使用（太細，雜訊太多） | — |
| `Debug` | dev 環境細節除錯 | 進入特定方法、變數值 |
| `Information` | 正常業務流程 | 使用者登入成功、寄送驗證信、新增 transaction |
| `Warning` | 業務錯誤（**所有 `ApiException` 都用這個**） | 密碼錯誤、信箱已存在、rate limit 觸發 |
| `Error` | 未預期例外、外部服務失敗 | DB 連線失敗、Redis 操作失敗、寄信失敗、500 錯誤 |
| `Critical` | 服務無法運作 | 應用程式啟動失敗、整個依賴掛掉 |

### 重要原則

- **`ApiException` 用 Warning，不要用 Error**：Error 保留給「需要 oncall 處理」的事情。業務錯誤（例如使用者密碼打錯）不應該觸發告警
- **dev 環境 MinimumLevel 用 `Debug`**，prod 用 `Information`，由 `appsettings.{Environment}.json` 控制
- **第三方 noise 要降級**：`Microsoft.AspNetCore` 與 `Microsoft.EntityFrameworkCore` 設成 `Warning`，否則每個 request 都會印一堆 routing / SQL log

---

## 結構化 Log 寫法

Serilog 的核心優勢是**結構化 log**——讓每筆 log 帶獨立欄位，未來能精準查詢。

### ❌ 錯誤寫法（字串拼接）

```csharp
_logger.LogInformation($"User {userId} logged in from {ip}");
```

問題：JSON 輸出只是一坨字串，無法針對 UserId 篩選。

### ✅ 正確寫法（template + 參數）

```csharp
_logger.LogInformation("User {UserId} logged in from {Ip}", userId, ip);
```

優勢：JSON log 會產生獨立欄位 `UserId`、`Ip`，未來在 Seq / Elastic 能用 `UserId="xxx"` 精準篩。

### Property naming 慣例

- 用 PascalCase（`UserId` 不是 `userId`、不是 `user_id`）
- 用具描述性名稱（`UserId` 不是 `Id`，`TransactionId` 不是 `Tid`）
- 物件用 `{@Object}` 強制序列化（一般 `{Object}` 會呼叫 `ToString()`）

```csharp
_logger.LogInformation("Created transaction {@Transaction}", transaction);
//                                          ^ 注意這個 @
```

---

## CorrelationId 設計

每個 HTTP request 應該有一個 `CorrelationId`，串起該 request 在所有層級的 log（middleware / controller / service / DB）。

### 流程

1. **`CorrelationIdMiddleware`**（pipeline 最外層）：
   - 讀取 request header `X-Correlation-Id`，若沒帶則產生新 GUID
   - 用 `LogContext.PushProperty("CorrelationId", correlationId)` 推入該 request 的 log scope
   - 在 response header 也回傳 `X-Correlation-Id` 方便前端對應

2. **後續所有 log** 自動帶 `CorrelationId` 欄位（透過 `Enrich.FromLogContext()`）

3. **prod 出問題追查**：使用者回報問題附 `X-Correlation-Id` → grep log 檔 → 整個 request 的 log timeline 一次撈出

### 中介層位置

`CorrelationIdMiddleware` 應放在 pipeline **最外層**（甚至比 `ExceptionHandlingMiddleware` 還外），確保連例外處理都能帶 CorrelationId。

```
Request → CorrelationIdMiddleware → ExceptionHandlingMiddleware → ... → Controller
```

---

## Request Logging

啟用 `app.UseSerilogRequestLogging()` 取代手動 log 每個 request，會自動產生：

```
HTTP GET /api/transaction responded 200 in 23.4521 ms
```

並可透過 `EnrichDiagnosticContext` 自訂額外欄位：

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagCtx, httpCtx) =>
    {
        if (httpCtx.HasUserId())
        {
            diagCtx.Set("UserId", httpCtx.GetUserId());
        }
        diagCtx.Set("UserAgent", httpCtx.Request.Headers.UserAgent.ToString());
    };
});
```

---

## 敏感資訊處理

**絕對不能寫進 log**：

- 使用者密碼（即使是 hash 後）
- 完整 JWT / Refresh Token 字串
- Email 驗證 token / 重設密碼 token
- 信用卡、身分證等個資

**可以寫進 log 但需注意**：

- Email、Name（debug 必要，但記得這些會落到檔案）
- IP（rate limit 與安全分析需要，但屬於個資）
- UserId（GUID，本身無語意，OK）

### 寄送驗證信的 log 範例

```csharp
// ❌ 不要
_logger.LogInformation("Sent verify email with token {Token} to {Email}", token, email);

// ✅ 應該
_logger.LogInformation("Sent verify email to {Email}, token expires at {ExpiresAt}", email, expiresAt);
```

---

## 檔案保留與大小限制

務必設定，否則 log 會無限長大塞爆硬碟：

| 設定 | 建議值 | 說明 |
|------|--------|------|
| `rollingInterval` | `Day` | 每天一個檔 |
| `fileSizeLimitBytes` | `104857600` (100 MB) | 單檔上限 |
| `rollOnFileSizeLimit` | `true` | 超過大小自動換檔 |
| `retainedFileCountLimit` | `null`（搭配下方手動清理） | Map sink 的 retainedFileCountLimit 行為與一般 file sink 不同，建議用排程腳本清理舊資料夾 |
| `shared` | `true` | 多 instance 共寫同檔（Docker 多副本場景） |

### 舊 log 清理

由於使用年/月/日資料夾結構，`retainedFileCountLimit` 不適用。建議：

- **dev 環境**：人工 `rm -rf logs/2025/` 即可
- **prod 環境**：寫個 cron job 每天刪除 N 天前的資料夾，或交給 Docker volume 管理

---

## Bootstrap Logger

`Program.cs` 開頭應建立 **bootstrap logger**，確保 `WebApplication.CreateBuilder` 之前的啟動失敗也能被記錄：

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, config) =>
        config.ReadFrom.Configuration(context.Configuration)
              .ReadFrom.Services(services));
    // ...
    var app = builder.Build();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
}
finally
{
    Log.CloseAndFlush();
}
```

---

## 與既有架構的整合

| 現有元件 | 變更 |
|---------|------|
| `Console.WriteLine`（如 UserService 印驗證連結） | 改為 `_logger.LogInformation` 結構化欄位 |
| `ExceptionHandlingMiddleware` | 用 `ILogger` 取代任何 `Console.Error.WriteLine`，`ApiException` 用 Warning，未預期例外用 Error |
| `LogActionFilter` | 已有的 filter 可保留，但建議重構為使用 `ILogger`（若還在用 Console） |
| 各層 Service / Controller | 注入 `ILogger<T>`，T 為該類別自身（讓 `SourceContext` 自動帶上） |

---

## .gitignore

`logs/` 資料夾整個加入 `.gitignore`：

```
# Logs
logs/
*.log
```

---

## 未來擴充方向（暫不做）

- **接 Seq / Elastic / CloudWatch**：prod 上線且有真實流量時再導入
- **Performance log**：用 `Stopwatch` 記錄關鍵 service 方法耗時，落地到 log 供分析
- **Sampling**：高流量時對 Information level 採樣（例如 10% 才寫），降低儲存成本
- **PII masking**：如果未來需要 log email / IP 但又有合規要求，加上 enricher 做 masking

---

## 相關文件

- [Serilog 官方文件](https://serilog.net/)
- [Serilog.Sinks.Map](https://github.com/serilog/serilog-sinks-map)
- [Compact JSON Formatter (CLEF)](https://github.com/serilog/serilog-formatting-compact)
