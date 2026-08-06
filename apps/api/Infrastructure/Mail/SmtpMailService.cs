using BusinessOS.Application.Abstraction;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace BusinessOS.Infrastructure.Mail;

public class SmtpMailService(IConfiguration configuration, ILogger<SmtpMailService> logger) : IMailService
{
    public async Task SendInviteEmailAsync(string to, string name, string companyName, string inviteLink, CancellationToken cancellationToken = default)
    {
        var host = configuration["SMTP_HOST"];
        var from = configuration["SMTP_FROM"] ?? "no-reply@businessos.local";

        if (string.IsNullOrEmpty(host))
        {
            logger.LogWarning("SMTP_HOST not configured - skipping invite email to {To} (link: {Link})", to, inviteLink);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = $"You've been invited to {companyName} on BusinessOS";
        message.Body = new TextPart("html")
        {
            Text = $"<p>Hi {name},</p>" +
                   $"<p>You've been invited to join <strong>{companyName}</strong> on BusinessOS.</p>" +
                   $"<p><a href=\"{inviteLink}\">Accept the invite</a></p>",
        };

        var port = int.TryParse(configuration["SMTP_PORT"], out var parsedPort) ? parsedPort : 587;

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);

        var user = configuration["SMTP_USER"];
        var pass = configuration["SMTP_PASS"];
        if (!string.IsNullOrEmpty(user))
        {
            await client.AuthenticateAsync(user, pass, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
