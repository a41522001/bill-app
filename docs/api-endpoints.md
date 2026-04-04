# API Endpoints

所有回應皆經 `ResultWrapFilter` 包裝為統一格式：

```json
{
  "data": T | null,
  "code": 0,
  "message": "成功",
  "time": "2026-04-03T12:00:00Z"
}
```

- `code: 0` = 成功，`code: 1` = 通用錯誤，其餘為模組專屬 code
- 錯誤時 `data` 為 `null`，`message` 帶錯誤訊息
- Auth 相關 cookie（`accessToken`、`refreshToken`）皆為 HttpOnly + Secure

---

## User API (`/api/user`)

### POST `/api/user/signup`

註冊帳號（Local）。

**Request Body:**

```json
{
  "name": "string",
  "email": "string",
  "password": "string"
}
```

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `"註冊成功請至信箱收取驗證信"` | 成功 |
| 400 | `"註冊失敗"` | — |

---

### POST `/api/user/login`

Local 帳號登入。成功時 Set-Cookie: `accessToken`、`refreshToken`。

**Request Body:**

```json
{
  "email": "string",
  "password": "string"
}
```

**Response:**

| HTTP Status | data | message | 備註 |
|-------------|------|---------|------|
| 200 | `"登入成功"` | 成功 | 回應帶 Set-Cookie |
| 400 | `null` | 帳號或密碼錯誤 | code: 1 |
| 400 | `null` | 該帳號已綁定 Google，請用 Google 登入 | **code: 1002** (`AccountBoundToGoogle`)，前端應引導至 Google 登入 |
| 400 | `null` | 信箱未驗證 | **code: 1001** (`EmailNotVerified`)，前端應顯示重送驗證信按鈕 |

---

### POST `/api/user/googleLogin`

Google OAuth 登入（ID Token 驗證）。成功時 Set-Cookie: `accessToken`、`refreshToken`。

**Request Body:**

```json
{
  "idToken": "string"
}
```

**Response:**

| HTTP Status | data | message | 備註 |
|-------------|------|---------|------|
| 200 | `"Google 登入成功"` | 成功 | 回應帶 Set-Cookie |
| 400 | `null` | 該 Email 已使用密碼註冊，請用密碼登入 | **code: 1003** (`AccountBoundToLocal`)，前端應引導至密碼登入 |

---

### POST `/api/user/logout`

登出，清除 cookie 及 Redis refresh token。

**Request Body:** 無（從 cookie 讀取 refreshToken）

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `"登出成功"` | 成功 |

---

### GET `/api/user/profile`

取得目前登入使用者資訊。**需要登入**（透過 middleware 驗證 cookie）。

**Request Body:** 無

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `UserProfileResponse` | 成功 |

**UserProfileResponse:**

```json
{
  "name": "string",
  "email": "string",
  "authProvider": 0,
  "isEmailVerified": true
}
```

- `authProvider`: `0` = Local, `1` = Google

---

### GET `/api/user/verifyEmail/{token:guid}`

驗證 Email（點擊驗證信連結）。

**Request:** URL path 帶 GUID token

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `"驗證成功"` | 成功 |
| 400 | `"驗證失敗"` | — |

---

### POST `/api/user/resendVerifyEmail`

重送驗證信。所有失敗情境（user 不存在、Google 帳號、已驗證、冷卻中）皆靜默返回相同回應，防止帳號枚舉。

**Request Body:**

```json
{
  "email": "string"
}
```

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `"若該信箱已註冊，驗證信已寄出"` | 成功 |

> 無論成功或失敗，回應皆相同（60 秒冷卻）。

---

### POST `/api/user/forgetPassword`

忘記密碼，寄送重設密碼信。所有失敗情境皆靜默返回相同回應，防止帳號枚舉。

**Request Body:**

```json
{
  "email": "string"
}
```

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `"若該信箱已註冊，重設密碼信已寄出"` | 成功 |

> 無論成功或失敗，回應皆相同（60 秒冷卻）。

---

### POST `/api/user/resetPassword`

重設密碼（透過忘記密碼信中的 token）。成功後強制所有裝置登出。

**Request Body:**

```json
{
  "password": "string",
  "token": "guid"
}
```

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `"重設密碼成功，請用新密碼登入"` | 成功 |
| 400 | `null` | 連結已失效，請重新申請 | 

> 收到 400 時，前端應引導用戶回忘記密碼頁重新申請。

---

## Category API (`/api/category`)

所有 Category API **需要登入**。

### POST `/api/category`

新增類別。同一用戶不可重複類別名稱。

**Request Body:**

```json
{
  "name": "string",
  "type": 0
}
```

- `type`: `0` = Income（收入）, `1` = Expense（支出）

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `"新增成功"` | 成功 |

---

### GET `/api/category`

取得該使用者所有未刪除的類別。

**Request Body:** 無

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `CategoryResponse[]` | 成功 |

**CategoryResponse:**

```json
[
  {
    "id": "guid",
    "name": "string",
    "typeName": "收入",
    "type": 0
  }
]
```

- `typeName`: `"收入"` (Income=0) / `"支出"` (Expense=1)

---

### DELETE `/api/category/{id:guid}`

軟刪除類別（設定 `DeletedAt` 時間戳）。

**Request:** URL path 帶 GUID id

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `"刪除成功"` | 成功 |

---

## Transaction API (`/api/transaction`)

所有 Transaction API **需要登入**。

### POST `/api/transaction`

新增交易記錄。Transaction 的 Type 繼承自所屬 Category 的 Type。

**Request Body:**

```json
{
  "categoryId": "guid",
  "amount": 100.50,
  "note": "string | null"
}
```

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `"新增成功"` | 成功 |
| 400 | `null` | 無此類別 |

---

### GET `/api/transaction`

查詢交易明細（分頁 + 篩選）。

**Query Parameters:**

| 參數 | 類型 | 必填 | 預設值 | 說明 |
|------|------|------|--------|------|
| `type` | int | 否 | — | `0` = Income, `1` = Expense |
| `categoryId` | guid | 否 | — | 篩選特定類別 |
| `startDate` | DateTime (UTC) | 否 | — | 起始時間（含），前端帶 ISO 8601 格式 |
| `endDate` | DateTime (UTC) | 否 | — | 結束時間（含），前端帶 ISO 8601 格式 |
| `page` | int | 否 | 1 | 頁碼 |
| `limit` | int | 否 | 10 | 每頁筆數 |

**Request 範例:**

```
GET /api/transaction?type=1&startDate=2024-12-11T16:00:00.000Z&endDate=2024-12-12T15:59:59.000Z&page=1&limit=10
```

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `PaginatedResponse<TransactionResponse>` | 成功 |

**PaginatedResponse\<TransactionResponse\>:**

```json
{
  "data": [
    {
      "id": "guid",
      "amount": 100.50,
      "note": "string | null",
      "createdAt": "2024-12-12T08:30:00Z",
      "type": 0,
      "typeName": "收入",
      "categoryId": "guid",
      "categoryName": "薪水"
    }
  ],
  "meta": {
    "total": 50,
    "page": 1,
    "limit": 10,
    "totalPages": 5
  }
}
```

- `type`: `0` = Income, `1` = Expense
- `typeName`: `"收入"` / `"支出"`

---

### GET `/api/transaction/typeList`

取得交易類型下拉選單選項。

**Request:** 無

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `SelectListDto[]` | 成功 |

**SelectListDto[]:**

```json
[
  { "title": "收入", "value": "0" },
  { "title": "支出", "value": "1" }
]
```

---

### DELETE `/api/transaction/{id:guid}`

刪除交易記錄（硬刪除）。

**Request:** URL path 帶 GUID id

**Response:**

| HTTP Status | data | message |
|-------------|------|---------|
| 200 | `"刪除成功"` | 成功 |
| 400 | `null` | 無此交易明細 |

---

## ResponseCode 一覽

| Code | 常數名稱 | 說明 | 前端處理建議 |
|------|----------|------|-------------|
| 0 | `Success` | 成功 | — |
| 1 | `Error` | 通用錯誤 | 顯示 message |
| 1001 | `EmailNotVerified` | 信箱未驗證 | 顯示重送驗證信按鈕 |
| 1002 | `AccountBoundToGoogle` | 該帳號已綁定 Google | 引導至 Google 登入 |
| 1003 | `AccountBoundToLocal` | 該 Email 已使用密碼註冊 | 引導至密碼登入 |

---

## 需要登入的 API

以下 API 需要有效的 `accessToken` cookie（或透過 `refreshToken` 自動續期）：

- `GET /api/user/profile`
- `POST /api/category`
- `GET /api/category`
- `DELETE /api/category/{id}`
- `POST /api/transaction`
- `GET /api/transaction`
- `GET /api/transaction/typeList`
- `DELETE /api/transaction/{id}`

## 不需要登入的 API（Whitelist）

- `POST /api/user/signup`
- `POST /api/user/login`
- `POST /api/user/logout`
- `POST /api/user/googleLogin`
- `GET /api/user/verifyEmail/{token}`
- `POST /api/user/resendVerifyEmail`
- `POST /api/user/forgetPassword`
- `POST /api/user/resetPassword`
