namespace Bill_App_API.Interfaces;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
}
