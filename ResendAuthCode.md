# 重送驗證信功能

## API

`POST /api/user/resendVerifyEmail`，body 帶 `email`，加進 middleware whitelist。

## Service 邏輯

1. 用 email 查 DB 找 user
2. 找不到 / AuthProvider 是 Google / 已驗證 → 統一回「若信箱正確，驗證信已寄出」（防枚舉）
3. 檢查 Redis `email:resendCooldown#{userId}` 是否存在 → 存在就拒絕（「請稍後再試」）
4. 產生新 GUID token → 存 Redis `email:verify#{token}` = userId（TTL 同設定）
5. 設 `email:resendCooldown#{userId}`（TTL 60 秒）
6. 透過 EmailService 寄送驗證信，連結指向前端路由 `{FRONT_END_URL}/verifyEmail/{token}`
7. Console.WriteLine 記錄驗證連結

## Redis 新增 key

| Key Pattern | Type | TTL | Purpose |
|---|---|---|---|
| `email:resendCooldown#{userId}` | String | 60s | 重送冷卻，防止短時間內重複請求 |

## 需要改的檔案

- `UserDto.cs` — 新增 `ResendVerifyEmailRequest(string Email)`
- `IUserService.cs` — 新增方法
- `UserService.cs` — 實作邏輯
- `UserController.cs` — 新增 endpoint
- `AccessTokenMiddleware.cs` / `RefreshTokenMiddleware.cs` — whitelist 加 `/api/user/resendVerifyEmail`
- `IRedisService.cs` / `RedisService.cs` / `RedisKey.cs` — 新增 cooldown key 的讀寫方法
