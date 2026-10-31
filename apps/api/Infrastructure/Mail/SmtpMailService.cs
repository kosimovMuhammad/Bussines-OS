using BusinessOS.Application.Abstraction;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace BusinessOS.Infrastructure.Mail;

public class SmtpMailService(IConfiguration configuration, ILogger<SmtpMailService> logger) : IMailService
{
    public async Task SendInviteEmailAsync(string to, string name, string companyName, string code, CancellationToken cancellationToken = default)
    {
        var settings = ReadSettings();
        if (settings is null)
        {
            logger.LogWarning("EmailSettings:Smtp:Host not configured - skipping invite email to {To} (code: {Code})", to, code);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.Value.FromName, settings.Value.FromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = $"You've been invited to {companyName} on BusinessOS";
        message.Body = new TextPart("html")
        {
            Text = $"<p>Hi {name},</p>" +
                   $"<p>You've been invited to join <strong>{companyName}</strong> on BusinessOS.</p>" +
                   $"<p>Your invite code is:</p>" +
                   $"<p style=\"font-size:24px;font-weight:bold;\">{code}</p>" +
                   $"<p>This code expires in 15 minutes.</p>",
        };

        await SendAsync(settings.Value, message, cancellationToken);
    }

    public async Task SendVerificationCodeEmailAsync(string to, string name, string code, CancellationToken cancellationToken = default)
    {
        var settings = ReadSettings();
        if (settings is null)
        {
            logger.LogWarning("EmailSettings:Smtp:Host not configured - skipping verification email to {To} (code: {Code})", to, code);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.Value.FromName, settings.Value.FromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = "Your BusinessOS verification code";
        message.Body = new TextPart("html")
        {
            Text = $"<p>Hi {name},</p>" +
                   $"<p>Your verification code is:</p>" +
                   $"<p style=\"font-size:24px;font-weight:bold;\">{code}</p>" +
                   $"<p>This code expires in 15 minutes.</p>",
        };

        await SendAsync(settings.Value, message, cancellationToken);
    }

    public async Task SendNotificationEmailAsync(string to, string name, string title, string body, CancellationToken cancellationToken = default)
    {
        var settings = ReadSettings();
        if (settings is null)
        {
            logger.LogWarning("EmailSettings:Smtp:Host not configured - skipping notification email to {To} ({Title})", to, title);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.Value.FromName, settings.Value.FromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = title;
        message.Body = new TextPart("html")
        {
            Text = $"<p>Hi {name},</p>" +
                   $"<p>{body}</p>",
        };

        await SendAsync(settings.Value, message, cancellationToken);
    }

    private async Task SendAsync(SmtpSettings settings, MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, settings.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.Auto, cancellationToken);

        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private SmtpSettings? ReadSettings()
    {
        var section = configuration.GetSection("EmailSettings");
        var host = section["Smtp:Host"];
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        var port = int.TryParse(section["Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        var useSsl = !bool.TryParse(section["Smtp:UseSsl"], out var parsedUseSsl) || parsedUseSsl;

        return new SmtpSettings(
            host,
            port,
            useSsl,
            section["Credentials:Username"],
            section["Credentials:Password"],
            section["Sender:Email"] ?? "no-reply@businessos.local",
            section["Sender:Name"] ?? "BusinessOS");
    }

    private readonly record struct SmtpSettings(string Host, int Port, bool UseSsl, string? Username, string? Password, string FromEmail, string FromName);
}
