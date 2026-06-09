using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;

namespace UrbanService.BLL.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(EmailMessageDto email, CancellationToken cancellationToken = default)
    {
        Validate(email);

        var host = _configuration["Smtp:Host"]
            ?? throw new InvalidOperationException("Missing config: Smtp:Host");
        var username = _configuration["Smtp:Username"]
            ?? throw new InvalidOperationException("Missing config: Smtp:Username");
        var password = _configuration["Smtp:Password"]
            ?? throw new InvalidOperationException("Missing config: Smtp:Password");
        var fromEmail = _configuration["Smtp:FromEmail"] ?? username;
        var fromName = _configuration["Smtp:FromName"] ?? "UrbanService";
        var port = int.TryParse(_configuration["Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        var enableSsl = !bool.TryParse(_configuration["Smtp:EnableSsl"], out var parsedSsl) || parsedSsl;

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = email.Subject,
            Body = email.Body,
            IsBodyHtml = email.IsHtml
        };

        AddRecipients(message.To, email.To);
        AddRecipients(message.CC, email.Cc);
        AddRecipients(message.Bcc, email.Bcc);

        foreach (var attachment in email.Attachments)
        {
            message.Attachments.Add(new Attachment(
                new MemoryStream(attachment.ContentBytes),
                attachment.FileName,
                attachment.ContentType));
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Credentials = new NetworkCredential(username, password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }

    private static void AddRecipients(MailAddressCollection target, IEnumerable<string> recipients)
    {
        foreach (var recipient in recipients.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            target.Add(recipient.Trim());
        }
    }

    private static void Validate(EmailMessageDto email)
    {
        if (email.To.Count == 0)
        {
            throw new ArgumentException("Email phải có ít nhất một người nhận.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(email.Subject))
        {
            throw new ArgumentException("Tiêu đề email là bắt buộc.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(email.Body))
        {
            throw new ArgumentException("Nội dung email là bắt buộc.", nameof(email));
        }
    }
}
