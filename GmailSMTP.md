# Gmail SMTP 寄信功能實作流程

## Gmail 設定步驟

### Step 1: 開啟兩步驟驗證

1. 登入 Google 帳號 → [安全性頁面](https://myaccount.google.com/security)
2. 找到「兩步驟驗證」→ 開啟（如果還沒開的話）

### Step 2: 產生應用程式密碼

1. 進入 [應用程式密碼頁面](https://myaccount.google.com/apppasswords)
2. 輸入應用程式名稱（例如 `Bill App`）
3. 點擊「建立」
4. 複製產生的 **16 位密碼**（只會顯示一次）

## 後端實作步驟

### Step 1: 安裝套件

```bash
dotnet add Bill-App-API package MailKit
```

### Step 2: 新增 SmtpOptions

**檔案**: `Bill-App-API/Options/SmtpOptions.cs`

```csharp
namespace Bill_App_API.Options;

public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "Bill App";
}
```

### Step 3: Program.cs 註冊 Options

```csharp
builder.Services.Configure<SmtpOptions>(options =>
{
    options.Host = Environment.GetEnvironmentVariable("SMTP__HOST") ?? "smtp.gmail.com";
    options.Port = int.Parse(Environment.GetEnvironmentVariable("SMTP__PORT") ?? "587");
    options.Email = Environment.GetEnvironmentVariable("SMTP__EMAIL") ?? "";
    options.Password = Environment.GetEnvironmentVariable("SMTP__PASSWORD") ?? "";
    options.DisplayName = Environment.GetEnvironmentVariable("SMTP__DISPLAY_NAME") ?? "Bill App";
});
```

### Step 4: 新增 EmailService

**檔案**: `Bill-App-API/Services/Interfaces/IEmailService.cs`

```csharp
namespace Bill_App_API.Interfaces;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
}
```

**檔案**: `Bill-App-API/Services/EmailService.cs`

```csharp
using Bill_App_API.Interfaces;
using Bill_App_API.Options;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Bill_App_API.Services;

public class EmailService(IOptions<SmtpOptions> smtpOptions) : IEmailService
{
    private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtpOptions.DisplayName, _smtpOptions.Email));
        message.To.Add(new MailboxAddress("", to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_smtpOptions.Host, _smtpOptions.Port, MailKit.Security.SecureSocketMode.StartTls);
        await client.AuthenticateAsync(_smtpOptions.Email, _smtpOptions.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
```

### Step 5: Program.cs 註冊 DI

```csharp
builder.Services.AddScoped<IEmailService, EmailService>();
```

### Step 6: 修改 UserService.Signup

將 `Console.WriteLine` 替換為寄信：

```csharp
// 原本
Console.WriteLine($"[DEV] 驗證連結: {url}");

// 改為
await emailService.SendAsync(
    req.Email,
    "Bill App - 驗證你的帳號",
    $"<h3>歡迎註冊 Bill App</h3><p>請點擊下方連結驗證你的信箱：</p><a href='{url}'>點擊驗證</a><p>此連結將在 {_userVerifyEmailOptions.TtlInHours} 小時後失效。</p>"
);
```

UserService constructor 需要注入 `IEmailService`。

### Step 7: 環境變數

`.env` 新增：

```
SMTP__HOST=smtp.gmail.com
SMTP__PORT=587
SMTP__EMAIL=your@gmail.com
SMTP__PASSWORD=xxxx xxxx xxxx xxxx
SMTP__DISPLAY_NAME=Bill App
```

`SMTP__PASSWORD` 填 Step 2 產生的 16 位應用程式密碼（含空格）。

## 注意事項

- Gmail SMTP 免費額度：**每日 500 封**（個人專案綽綽有餘）
- 應用程式密碼需要先開啟兩步驟驗證才能使用
- 寄件人會顯示你的 Gmail 地址，如果不想曝光可以申請專案用的 Gmail
- 之後上 production 可以換成 Resend / AWS SES，只需要改環境變數即可
