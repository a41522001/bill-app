# AWS S3 + CloudFront 圖片儲存設定

頭像圖片在生產環境改存 S3、透過 CloudFront 對外 serve。本檔記錄 AWS 端設定步驟與後端整合點。

## 架構

```
[使用者瀏覽器] ──GET──> [CloudFront CDN] ──OAC──> [S3 (private bucket)]
                              ↑
                              └─ 對外 avatar URL，DB 存這個

[.NET API] ──PutObject (IAM access key)──> [S3]
            上傳/刪除走後端，不經 CloudFront
```

- S3 bucket 全 private（Block all public access 全開），不直接對外
- CloudFront 透過 **OAC (Origin Access Control)** 讀取 S3
- 後端用 IAM user 的 access key 上傳/刪除物件
- DB 儲存 CloudFront 完整 URL，前端可直接 `<img src>`

## AWS 設定步驟

### Step 1: 建立 S3 Bucket

1. AWS Console → S3 → 建立儲存貯體
2. 區域：建議 `ap-northeast-1`（東京）或 `ap-southeast-1`（新加坡）
3. 名稱：全球唯一，建議 `bill-app-avatars-dev` / `bill-app-avatars-prod`
4. **物件擁有權**：`ACL 已停用（建議）`
5. **封鎖公開存取設定**：保持「封鎖所有公開存取」全勾選
6. 版本控制：停用（個人專案省成本）
7. 預設加密：`SSE-S3` + 啟用 Bucket Key（不要選 SSE-KMS，會額外計費）

> 不需要事先建「資料夾」。S3 沒有真的資料夾，console 看到的是 key 中 `/` 的視覺渲染；後端上傳時 key = `avatars/{guid}_xxx.webp`，console 自然會顯示一個 `avatars/` 資料夾。

### Step 2: 建立應用程式專用 IAM User

不要用管理者帳號的 access key 給程式跑，要建獨立 user 並套用最小權限。

1. IAM → 使用者 → 建立使用者，名稱 `bill-app-api-dev`
2. **不要勾**「提供使用者對 AWS Management Console 的存取權」（純程式用，不需要密碼）
3. 權限選項：直接附加政策 → 建立政策（JSON 模式）：

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:PutObject",
        "s3:GetObject",
        "s3:DeleteObject"
      ],
      "Resource": "arn:aws:s3:::bill-app-avatars-dev/*"
    }
  ]
}
```

4. 政策名稱：`BillAppAvatarsDevAccess` → 建立 → 套用到 user

### Step 3: 產生 Access Key

1. 進入該 user → 安全憑證 tab → 建立存取金鑰
2. 使用案例選「應用程式在 AWS 外部執行」
3. 建立後會顯示 **Access Key ID** + **Secret Access Key**（**Secret 只顯示一次**，務必存下來）
4. 寫進 `.env`：
   ```env
   AWS_ACCESS_KEY_ID=AKIA...
   AWS_SECRET_ACCESS_KEY=...
   AWS_REGION=ap-northeast-1
   ```

> Access key 萬一外洩：立刻到 IAM 把那組 key 設為非作用中並刪除，重建一組。AWS 有自動掃 GitHub 的服務，commit 上去通常 5–10 分鐘內會收到警告信並自動停用，但已被挖礦的損失要自己付。

### Step 4: 建立 CloudFront Distribution

1. CloudFront → 建立分配
2. **來源網域**：選剛才的 S3 bucket
3. **來源存取**：`Origin access control settings (recommended)` → 建立新的 OAC（不要用舊的 OAI）
4. CloudFront 會產生一段 bucket policy → 複製，**貼回 S3 bucket 的「許可」→「儲存貯體政策」**
5. **檢視器通訊協定政策**：`Redirect HTTP to HTTPS`
6. **價格類別**：個人專案選「僅使用北美和歐洲」最便宜；要包含亞洲流量則選含亞洲的方案
7. 建立後等 5–15 分鐘 propagate，記下 distribution domain（如 `d1234abcd.cloudfront.net`）
8. 寫進 `.env`：
   ```env
   CLOUD_FRONT_URL=https://d1234abcd.cloudfront.net
   ```

> 結尾不要加 `/`，後端組路徑時會自己拼。

### Step 5: AWS Budgets 設定預算告警

第一次用 AWS 強烈建議：AWS Billing → Budgets → 建立月預算（如 $5）→ 達 80% / 100% 寄信通知。即使只是學習也要設，避免意外帳單。

## 後端整合

### Step 1: 安裝套件

```bash
dotnet add Bill-App-API package AWSSDK.S3
```

### Step 2: 新增 Options

**`Bill-App-API/Options/AwsOptions.cs`**

```csharp
namespace Bill_App_API.Options;

public class AwsOptions
{
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public string Region { get; set; } = "";
}
```

**`Bill-App-API/Options/S3Options.cs`**

```csharp
namespace Bill_App_API.Options;

public class S3Options
{
    public string BucketName { get; set; } = "";
    public string AvatarFolder { get; set; } = "";
    public string CloudFrontUrl { get; set; } = "";
}
```

### Step 3: Program.cs DI 註冊

```csharp
var storageProvider = Environment.GetEnvironmentVariable("STORAGE_PROVIDER") ?? "Local";
if (storageProvider == "S3")
{
    builder.Services.Configure<AwsOptions>(options =>
    {
        options.AccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";
        options.SecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";
        options.Region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "";
    });
    builder.Services.Configure<S3Options>(options =>
    {
        options.BucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME") ?? "";
        options.AvatarFolder = Environment.GetEnvironmentVariable("S3_BUCKET_AVATAR_FOLDER") ?? "";
        options.CloudFrontUrl = Environment.GetEnvironmentVariable("CLOUD_FRONT_URL") ?? "";
    });
    builder.Services.AddSingleton<IAmazonS3>(sp =>
    {
        var opts = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
        return new AmazonS3Client(
            opts.AccessKeyId,
            opts.SecretAccessKey,
            Amazon.RegionEndpoint.GetBySystemName(opts.Region));
    });
    builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
}
```

### 重要設計決策

- **`IAmazonS3` 是 Singleton**：AWS SDK client 設計成全應用程式共用一個實例，內部維護 connection pool，跟 `IConnectionMultiplexer` 同樣概念。註冊成 Scoped 會浪費連線
- **顯式從 `AwsOptions` 取憑證**：不依賴 SDK 預設環境變數讀取，跟專案其他 Options pattern 一致，未來改用 Secrets Manager 等來源時只要改 Options 來源
- **DI 註冊整段包在 `if (storageProvider == "S3")`**：Local 模式啟動時不會因為缺少 AWS 環境變數而炸掉

### S3FileStorageService 實作要點

- **`PutObjectRequest` 用 `InputStream`，不要用 `ContentBody`（給字串用）或 `FilePath`（給本機磁碟檔案用）**
- **每張縮圖用獨立 `MemoryStream`**：兩張圖共用同一個 stream + `Position = 0` 會踩到「殘留 byte」的雷（短的 thumb 寫入後尾巴還是舊 original 內容）
- **`ms.Position = 0`**：`SaveAsWebpAsync` 寫完後 stream 位置在尾端，必須重設才能讓 S3 讀到完整內容
- **`Key` 包含「資料夾」前綴**：S3 沒有真的資料夾，靠 key 中的 `/` 模擬，例如 `Key = "avatars/{guid}_original.webp"`
- **回傳完整 CloudFront URL**：`return $"{_s3Options.CloudFrontUrl}/{key}"`，DB 直接存這個字串，前端不用拼接
- **`DeleteAsync` 從 URL 還原 Key**：DB 存的是完整 URL，刪除時需要 `path.Replace($"{cloudFrontUrl}/", "")` 拔掉前綴拿到 S3 key

## 環境變數總覽

```env
STORAGE_PROVIDER=S3
AWS_ACCESS_KEY_ID=AKIA...
AWS_SECRET_ACCESS_KEY=...
AWS_REGION=ap-northeast-1
S3_BUCKET_NAME=bill-app-avatars-dev
S3_BUCKET_AVATAR_FOLDER=avatars
CLOUD_FRONT_URL=https://d1234abcd.cloudfront.net
```

## 常見錯誤

| 錯誤訊息 / 症狀 | 原因 | 解法 |
|------|------|------|
| `Please specify one of either an InputStream or a FilePath to be PUT as an S3 object` | `PutObjectRequest` 沒設 `InputStream` 或 `FilePath` | 用 `InputStream = ms` |
| 上傳成功但 thumb 圖片開不開或檔案異常大 | 兩張圖共用同一 `MemoryStream`，舊 byte 殘留 | 改用兩個獨立 `MemoryStream`（或 `ms.SetLength(0); ms.Position = 0`） |
| CloudFront 回 403 | bucket policy 沒貼 OAC 產生的那段 / OAC 沒綁好 | 重新到 CloudFront distribution 設定 → 複製 OAC bucket policy 貼到 S3 |
| CloudFront 回 404，但物件確實存在 | distribution 還在 propagate（建立後 5–15 分鐘） | 等一下再試 |
| SDK 報 region 相關錯誤 | `AWS_REGION` 跟 bucket 實際 region 不一致 | 對齊 — IAM 是全球服務沒有 region，以 S3 bucket 為準 |

## 安全提醒

- `.env` 必須在 `.gitignore` 內，**永遠不要 commit access key**
- IAM user policy 一定要限定 `Resource: arn:aws:s3:::特定bucket/*`，不要給 `*`
- IAM user 不開 console access（純程式用）
- 不確定的 IAM user → 看 Created 時間 + Last activity，懷疑非自己建的就刪掉重建
- bucket 永遠保持 Block all public access 全開，所有對外讀取走 CloudFront
