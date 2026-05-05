# Docker 化 + CD Pipeline 實作指南

> 目標：把 Bill-App API 容器化，建立自動部署流程（push to main → 自動部署到 Linode VPS）

## 整體架構

```
開發者 push to main
       ↓
GitHub Actions CI（restore → build → test）
       ↓ CI 通過
GitHub Actions CD
  ├── Build Docker Image
  ├── Push to ghcr.io（GitHub Container Registry）
  └── SSH 到 Linode VPS
       ├── docker compose pull（拉新 image）
       └── docker compose up -d（重啟 API）
```

**VPS 上的服務架構：**

```
Linode VPS
├── bill-app-api     (port 8080)  ← 你的 .NET API container
├── bill-app-postgres (內部 5432) ← 只有 API 能連，外部不暴露
└── bill-app-redis   (內部 6379) ← 只有 API 能連，外部不暴露
```

---

## Step 1：修改 Program.cs（讓連線可配置）

### 為什麼要改？

目前 `Program.cs` 有兩個問題：

1. **DB 連線寫死 `Host=localhost`** — Docker 容器內，PostgreSQL 的 hostname 不是 localhost，是 Docker Compose 的 service name（`db`）
2. **Redis 連線寫死 `localhost:6379`** — 同理，容器內要用 `redis:6379`
3. **`Env.Load("../.env")` 會報錯** — Docker 容器內沒有 `../.env` 這個檔案

### 你需要做什麼

打開 `Bill-App-API/Program.cs`，做以下 4 處修改：

#### 1-1. Env.Load 加判斷

找到這行：

```csharp
Env.Load("../.env");
```

改成：

```csharp
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "../.env");
if (File.Exists(envPath))
    Env.Load(envPath);
```

**原理**：本地開發時 `.env` 存在，正常 load；Docker 容器內不存在，就跳過（環境變數由 Docker Compose 注入）。

#### 1-2. DB 連線改用環境變數

找到這段：

```csharp
var connectionString = $"Host=localhost;Port=5432;Database={dbName};Username={dbUser};Password={dbPassword}";
```

改成：

```csharp
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";
```

**原理**：新增 `DB_HOST` 和 `DB_PORT` 環境變數，沒設定時預設 localhost（本地開發不用改 `.env`）。

#### 1-3. Redis 連線改用環境變數

找到這段：

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));
```

改成：

```csharp
var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnection));
```

#### 1-4. 加入 Health Endpoint + 條件式 HTTPS + 自動 Migration

找到 `var app = builder.Build();` 這行，在它後面加入自動 migration：

```csharp
var app = builder.Build();

// 自動執行 EF Core migration（Production 環境）
if (!app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BillDbContext>();
    db.Database.Migrate();
}
```

找到 `app.UseHttpsRedirection();` 這行，改成只在開發環境啟用：

```csharp
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
```

**原理**：Production 環境的 HTTPS 由前面的 reverse proxy（如 Nginx/Caddy）處理，API 容器本身只跑 HTTP。

在 `app.MapControllers();` 前面加入 health endpoint：

```csharp
app.MapGet("/health", () => Results.Ok("healthy"));
app.MapControllers();
```

**原理**：Docker Compose 的 healthcheck 會定期呼叫這個 endpoint，確認 API 是否正常運作。

### 驗證

改完後在本地跑 `dotnet run --project Bill-App-API`，確認一切照舊正常運作（因為預設值都是 localhost）。

---

## Step 2：建立 Dockerfile

### Multi-Stage Build 是什麼？

Docker image 分兩階段建立：
1. **Build stage**：用完整的 SDK image（大，約 900MB）來編譯程式碼
2. **Runtime stage**：只用輕量的 ASP.NET Runtime image（小，約 100MB）來跑程式

最終 image 只包含 runtime + 你的程式碼，不含 SDK 和原始碼。

### 你需要做什麼

在**專案根目錄**（跟 `Bill-App.sln` 同一層）建立 `Dockerfile`：

```dockerfile
# ===== Stage 1: Build =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先 copy csproj 和 sln，利用 Docker layer cache
# 只有這些檔案變動時才會重新 restore（省時間）
COPY Bill-App.sln .
COPY Bill-App-API/Bill-App-API.csproj Bill-App-API/
COPY Bill-App-Cache/Bill-App-Cache.csproj Bill-App-Cache/
RUN dotnet restore

# 複製所有原始碼，進行 publish
COPY Bill-App-API/ Bill-App-API/
COPY Bill-App-Cache/ Bill-App-Cache/
RUN dotnet publish Bill-App-API/Bill-App-API.csproj -c Release -o /app/publish

# ===== Stage 2: Runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 建立 avatars 目錄，之後會掛 volume
RUN mkdir -p /app/wwwroot/avatars

# 從 build stage 複製編譯好的檔案
COPY --from=build /app/publish .

# .NET 10 容器預設 port 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "Bill-App-API.dll"]
```

### 為什麼 Dockerfile 放在根目錄？

因為 `Bill-App-API.csproj` 參考了 `../Bill-App-Cache/Bill-App-Cache.csproj`，Docker build context 必須能同時看到兩個專案，所以 Dockerfile 放在根目錄（跟 `.sln` 同層）。

### Docker Layer Cache 原理

```
COPY *.csproj → RUN dotnet restore → COPY 原始碼 → RUN publish
      ↑                                    ↑
  很少變動（cache 命中率高）          經常變動（每次都要重建）
```

先 copy csproj 再 restore，這樣只有新增/移除 NuGet 套件時才會重跑 restore，平常只改程式碼時直接從 cache 繼續。

### 驗證

```bash
docker build -t bill-app-api .
```

成功的話會看到 `Successfully tagged bill-app-api:latest`。

可以檢查 image 大小：

```bash
docker images bill-app-api
```

應該在 200MB 左右（而不是 1GB+）。

---

## Step 3：建立 .dockerignore

### 為什麼需要？

`docker build` 會把整個目錄（build context）送給 Docker daemon。`.dockerignore` 排除不需要的檔案，加快 build 速度，也防止敏感檔案（如 `.env`）被打包進 image。

### 你需要做什麼

在**專案根目錄**建立 `.dockerignore`：

```
**/.git
**/.vs
**/.vscode
**/.claude
**/bin
**/obj
**/.env
**/.env.*
*.user
*.userosscache
*.sln.docstates
.DS_Store
docs/
Bill-App-Tests/
.github/
.gitignore
.dockerignore
```

### 重點

- `Bill-App-Tests/` 排除 — 測試專案不需要打包進 production image
- `.env` 排除 — 絕對不能讓 secrets 進到 image 裡
- `docs/` 排除 — 文件不需要

---

## Step 4：建立 docker-compose.prod.yml

### 跟現有 docker-compose.yml 的差異

| | `docker-compose.yml`（開發） | `docker-compose.prod.yml`（生產） |
|---|---|---|
| API | 不包含（本地 `dotnet run`） | 包含，用 ghcr.io image |
| DB port | 暴露 5432 | 不暴露（只有 API 能連） |
| Redis port | 暴露 6379 | 不暴露 |
| Healthcheck | 無 | 有 |
| Restart | always | unless-stopped |

### 你需要做什麼

在**專案根目錄**建立 `docker-compose.prod.yml`：

```yaml
services:
  db:
    image: postgres:18.1
    container_name: bill-app-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
      POSTGRES_DB: ${DB_NAME}
    volumes:
      - postgres_vol:/var/lib/postgresql/data
    networks:
      - bill-app-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${DB_USER} -d ${DB_NAME}"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7.4
    container_name: bill-app-redis
    restart: unless-stopped
    volumes:
      - redis_vol:/data
    command: redis-server --appendonly yes
    networks:
      - bill-app-network
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  api:
    image: ghcr.io/<YOUR_GITHUB_USERNAME>/bill-app-api:latest  # ← 換成你的 GitHub 帳號
    container_name: bill-app-api
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      DB_HOST: db
      DB_PORT: "5432"
      DB_USER: ${DB_USER}
      DB_PASSWORD: ${DB_PASSWORD}
      DB_NAME: ${DB_NAME}
      REDIS_CONNECTION: redis:6379
      JWT__KEY: ${JWT__KEY}
      JWT__ISSUER: ${JWT__ISSUER}
      JWT__AUDIENCE: ${JWT__AUDIENCE}
      JWT__DURATION_IN_MINUTES: ${JWT__DURATION_IN_MINUTES:-15}
      REFRESH_TOKEN__DURATION_IN_DAY: ${REFRESH_TOKEN__DURATION_IN_DAY:-7}
      REFRESH_TOKEN__OLD_TOKEN_GRACE_IN_SECONDS: ${REFRESH_TOKEN__OLD_TOKEN_GRACE_IN_SECONDS:-15}
      APP_DOMAIN: ${APP_DOMAIN}
      FRONT_END_URL: ${FRONT_END_URL}
      GOOGLE_AUTH_CLIENT_ID: ${GOOGLE_AUTH_CLIENT_ID}
      SMTP_HOST: ${SMTP_HOST}
      SMTP_PORT: ${SMTP_PORT:-587}
      SMTP_SENDER_EMAIL: ${SMTP_SENDER_EMAIL}
      SMTP_SENDER_NAME: ${SMTP_SENDER_NAME}
      SMTP_SENDER_PASSWORD: ${SMTP_SENDER_PASSWORD}
    volumes:
      - avatars_vol:/app/wwwroot/avatars
    networks:
      - bill-app-network
    depends_on:
      db:
        condition: service_healthy
      redis:
        condition: service_healthy

networks:
  bill-app-network:
    driver: bridge

volumes:
  postgres_vol:
  redis_vol:
  avatars_vol:
```

### 關鍵設計解說

**`DB_HOST: db` 和 `REDIS_CONNECTION: redis:6379`**

Docker Compose 會自動建立一個內部 DNS，service name 就是 hostname。所以 `db` 會解析到 PostgreSQL 容器的 IP，`redis` 會解析到 Redis 容器的 IP。

**`depends_on` + `condition: service_healthy`**

API 容器會等 DB 和 Redis 的 healthcheck 都通過才啟動，避免 API 啟動時 DB 還沒準備好導致連線失敗。

**DB/Redis 不暴露 port**

生產環境只有 API 需要對外（port 8080），DB 和 Redis 只在 Docker 內部網路通訊，更安全。

**`${VAR:-default}` 語法**

`${SMTP_PORT:-587}` 表示如果 `.env` 沒設定 `SMTP_PORT`，就用 587。

**`avatars_vol` Named Volume**

頭像圖片存在 Docker named volume 裡，即使 API 容器重建（deploy 新版本），圖片都還在。

> **使用 S3 + CloudFront 時可省略**：若 `STORAGE_PROVIDER=S3`，圖片不再寫入容器本機磁碟，可移除 `avatars_vol`、`api.volumes` 中的 `avatars_vol:/app/wwwroot/avatars`、以及 Dockerfile 裡的 `RUN mkdir -p /app/wwwroot/avatars`。改在 `api.environment` 加上 `STORAGE_PROVIDER`、`AWS_*`、`S3_*`、`CLOUD_FRONT_URL` 等變數即可。詳見 `docs/S3Storage.md`。

### 別忘了修復現有的 docker-compose.yml

現有的 postgres volume 掛載路徑有誤：

```yaml
# 錯的
- postgres_vol:/var/lib/postgresql

# 對的
- postgres_vol:/var/lib/postgresql/data
```

PostgreSQL 的資料實際存在 `/var/lib/postgresql/data`，掛錯路徑可能導致資料沒被持久化。

---

## Step 5：建立 CD Pipeline

### 流程說明

```
push to main
     ↓
ci.yml 跑 CI（restore → build → test）
     ↓ CI 通過（workflow_run 觸發）
cd.yml 跑 CD
  ├── Job 1: build-and-push
  │     ├── checkout 程式碼
  │     ├── 登入 ghcr.io（用 GITHUB_TOKEN，自動提供）
  │     ├── docker build
  │     └── docker push（tags: latest + git SHA）
  │
  └── Job 2: deploy（需要 Job 1 成功）
        ├── SSH 連到 Linode VPS
        ├── docker login ghcr.io（用 PAT）
        ├── docker compose pull api（拉新 image）
        ├── docker compose up -d（重啟）
        └── docker image prune -f（清理舊 image）
```

### 你需要做什麼

建立 `.github/workflows/cd.yml`：

```yaml
name: cd

on:
  workflow_run:
    workflows: ["ci"]
    types:
      - completed
    branches: [main]

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository_owner }}/bill-app-api

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    # 只在 CI 成功時才跑
    if: ${{ github.event.workflow_run.conclusion == 'success' }}
    permissions:
      contents: read
      packages: write

    steps:
      - uses: actions/checkout@v4

      - name: Log in to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Extract metadata (tags, labels)
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
          tags: |
            type=sha,prefix=
            type=raw,value=latest

      - name: Build and push Docker image
        uses: docker/build-push-action@v6
        with:
          context: .
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}

  deploy:
    runs-on: ubuntu-latest
    needs: build-and-push

    steps:
      - name: Deploy to Linode VPS
        uses: appleboy/ssh-action@v1
        with:
          host: ${{ secrets.LINODE_HOST }}
          username: ${{ secrets.LINODE_USER }}
          key: ${{ secrets.LINODE_SSH_KEY }}
          script: |
            cd ~/bill-app
            echo ${{ secrets.GHCR_TOKEN }} | docker login ghcr.io -u ${{ secrets.GHCR_USER }} --password-stdin
            docker compose -f docker-compose.prod.yml pull api
            docker compose -f docker-compose.prod.yml up -d
            docker image prune -f
```

### 關鍵概念解說

**`workflow_run` 觸發器**

CD 不是直接在 push 時觸發，而是**等 CI workflow 跑完且成功**才觸發。`if: github.event.workflow_run.conclusion == 'success'` 確保 CI 失敗時不會部署。

**`GITHUB_TOKEN` vs GitHub PAT**

| | `GITHUB_TOKEN` | Personal Access Token (PAT) |
|---|---|---|
| 哪裡用 | GitHub Actions 裡 push image | VPS 上 pull image |
| 怎麼來 | GitHub 自動提供，不用設定 | 手動產生，存到 Secrets |
| 權限 | `packages:write`（推 image） | `read:packages`（拉 image） |

**Image Tags**

每次 build 會產生兩個 tag：
- `latest` — docker-compose.prod.yml 用這個
- `abc1234`（git SHA）— 萬一出問題可以 rollback 到特定版本

**`docker image prune -f`**

每次部署完清理舊的 dangling images，避免 VPS 硬碟被塞滿。

---

## Step 6：設定 GitHub Secrets

到你的 GitHub repo → Settings → Secrets and variables → Actions → New repository secret

| Secret 名稱 | 值 | 說明 |
|---|---|---|
| `LINODE_HOST` | VPS 的 IP（如 `123.456.789.0`） | SSH 連線用 |
| `LINODE_USER` | SSH 使用者名稱（如 `root`） | SSH 連線用 |
| `LINODE_SSH_KEY` | SSH 私鑰的完整內容 | 貼上 `cat ~/.ssh/id_ed25519` 的輸出 |
| `GHCR_USER` | 你的 GitHub 帳號 | VPS pull image 用 |
| `GHCR_TOKEN` | GitHub PAT | VPS pull image 用 |

### 產生 GitHub PAT

1. GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
2. Generate new token
3. 勾選 `read:packages`
4. 產生後複製，存到 `GHCR_TOKEN` secret

### 產生 SSH Key（如果 VPS 還沒有）

```bash
# 在本機產生
ssh-keygen -t ed25519 -C "github-actions-deploy"

# 把公鑰加到 VPS
ssh-copy-id -i ~/.ssh/id_ed25519.pub user@your-linode-ip

# 把私鑰內容貼到 GitHub Secret LINODE_SSH_KEY
cat ~/.ssh/id_ed25519
```

---

## Step 7：VPS 前置準備（一次性）

SSH 到你的新 Linode VPS：

```bash
ssh root@your-linode-ip
```

### 7-1. 安裝 Docker

```bash
# Ubuntu/Debian
curl -fsSL https://get.docker.com | sh

# 驗證
docker --version
docker compose version
```

### 7-2. 建立專案目錄

```bash
mkdir -p ~/bill-app
cd ~/bill-app
```

### 7-3. 上傳 docker-compose.prod.yml

從本機複製到 VPS：

```bash
# 在本機執行
scp docker-compose.prod.yml root@your-linode-ip:~/bill-app/
```

### 7-4. 建立 .env（production 環境變數）

```bash
# 在 VPS 上
nano ~/bill-app/.env
```

填入 production 的值（強密碼、正式 domain 等）：

```env
DB_USER=billapp
DB_PASSWORD=<用強密碼>
DB_NAME=billapp

JWT__KEY=<至少 32 字元的隨機字串>
JWT__ISSUER=bill-app
JWT__AUDIENCE=bill-app

APP_DOMAIN=https://your-domain.com
FRONT_END_URL=https://your-frontend-domain.com

GOOGLE_AUTH_CLIENT_ID=<your-google-client-id>

SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_SENDER_EMAIL=<your-email>
SMTP_SENDER_NAME=Bill App
SMTP_SENDER_PASSWORD=<your-app-password>
```

### 7-5. 首次啟動

```bash
cd ~/bill-app

# 先登入 ghcr.io
echo YOUR_PAT | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin

# 啟動所有服務
docker compose -f docker-compose.prod.yml up -d

# 檢查狀態
docker compose -f docker-compose.prod.yml ps

# 看 API log
docker logs bill-app-api
```

---

## 驗證清單

完成所有步驟後，逐一確認：

- [ ] 本地 `dotnet run --project Bill-App-API` 正常運作（Program.cs 向後相容）
- [ ] 本地 `dotnet test Bill-App-Tests` 全部通過
- [ ] `docker build -t bill-app-api .` 建置成功
- [ ] `docker images bill-app-api` 大小合理（< 300MB）
- [ ] Push to main → CI 通過 → CD 觸發
- [ ] ghcr.io 上看到新的 image
- [ ] VPS 上 `docker compose ps` 三個服務都 healthy
- [ ] `curl http://your-vps-ip:8080/health` 回傳 200
- [ ] API 功能正常（登入、新增交易等）

---

## 未來可升級

完成基本 CD 後，可以進一步加入：

1. **Reverse Proxy（Nginx/Caddy）**— 處理 HTTPS、domain name、load balancing
2. **Watchtower** — 自動偵測 image 更新並重啟容器（可取代 SSH deploy 步驟）
3. **GitHub Environments** — 區分 staging / production，production 需要手動 approve
4. **Docker Compose profiles** — 用 `--profile monitoring` 選擇性啟動監控工具
5. **DB Backup** — cron job 定期 `pg_dump` 備份 PostgreSQL
