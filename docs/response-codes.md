# Response Code 定義

API 統一回傳格式 `ResponseWrap<T>` 中的 `Code` 欄位定義。

## 判斷邏輯

- `Code === 0`：成功
- `Code !== 0`：失敗（前端可依 Code 值做對應 UI 處理）

## 號碼段分配

| 範圍 | 模組 | 說明 |
|------|------|------|
| 0–999 | General | 通用狀態碼 |
| 1001–1999 | Auth | 驗證 / 登入 / 註冊相關 |
| 2001–2999 | Category | 類別相關 |
| 3001–3999 | Transaction | 交易相關 |

## General (0–999)

| Code | 常數名稱 | 說明 |
|------|----------|------|
| 0 | `Success` | 請求成功 |
| 1 | `Error` | 通用錯誤（無需特殊處理的失敗） |

## Auth (1001–1999)

| Code | 常數名稱 | 說明 | 前端建議處理 |
|------|----------|------|-------------|
| 1001 | `EmailNotVerified` | 信箱未驗證 | 顯示「重送驗證信」按鈕 |
| 1002 | `AccountBoundToGoogle` | 該帳號已綁定 Google，請用 Google 登入 | 引導至 Google 登入 |
| 1003 | `AccountBoundToLocal` | 該 Email 已使用密碼註冊，請用密碼登入 | 引導至密碼登入 |

## Category (2001–2999)

> 尚未定義，待功能擴充時新增。

## Transaction (3001–3999)

> 尚未定義，待功能擴充時新增。

## 新增規範

1. 新增 Code 時，同步更新 `Bill-App-API/Enums/ResponseCode.cs` 和本文件
2. 同一模組內的 Code 值依序遞增，不可重複使用已廢棄的號碼
3. C# 常數名稱使用 PascalCase，須能清楚表達錯誤情境
