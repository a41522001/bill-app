using Bill_App_API.Interfaces;
using Bill_App_API.Options;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Security;
namespace Bill_App_API.Services;

public class EmailService(IOptions<SmtpOptions> smtpOptions) : IEmailService
{
    private readonly SmtpOptions _smtpOptions = smtpOptions.Value;
    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtpOptions.SenderName, _smtpOptions.SenderEmail));
        message.To.Add(new MailboxAddress("", to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };
        using var client = new SmtpClient();
        await client.ConnectAsync(_smtpOptions.Host, _smtpOptions.Port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_smtpOptions.SenderEmail, _smtpOptions.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
