using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;

namespace UrbanService.BLL.Services;

public class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BrevoEmailSender> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessageDto email, CancellationToken cancellationToken = default)
    {
        Validate(email);

        var apiKey = _configuration["Brevo:ApiKey"];
        var senderEmail = _configuration["Brevo:SenderEmail"];
        var senderName = _configuration["Brevo:SenderName"] ?? "UrbanService";

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(senderEmail))
        {
            throw new InvalidOperationException("Brevo chưa được cấu hình đầy đủ.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email");
        request.Headers.Add("api-key", apiKey);
        request.Content = JsonContent.Create(new BrevoEmailRequest
        {
            Sender = new BrevoContact { Email = senderEmail, Name = senderName },
            To = MapContacts(email.To),
            Cc = email.Cc.Count == 0 ? null : MapContacts(email.Cc),
            Bcc = email.Bcc.Count == 0 ? null : MapContacts(email.Bcc),
            Subject = email.Subject,
            HtmlContent = email.IsHtml ? email.Body : null,
            TextContent = email.IsHtml ? null : email.Body,
            Attachments = email.Attachments.Count == 0
                ? null
                : email.Attachments.Select(a => new BrevoAttachment
                {
                    Name = a.FileName,
                    Content = Convert.ToBase64String(a.ContentBytes)
                }).ToList()
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Brevo email failed. StatusCode={StatusCode}, Response={Response}",
            (int)response.StatusCode,
            responseBody);

        throw new Exception($"Không thể gửi email qua Brevo. HTTP {(int)response.StatusCode}.");
    }

    private static IReadOnlyCollection<BrevoContact> MapContacts(IEnumerable<string> emails) =>
        emails.Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => new BrevoContact { Email = email.Trim() })
            .ToList();

    private static void Validate(EmailMessageDto email)
    {
        if (email.To.Count == 0)
            throw new ArgumentException("Email phải có ít nhất một người nhận.", nameof(email));
        if (string.IsNullOrWhiteSpace(email.Subject))
            throw new ArgumentException("Tiêu đề email là bắt buộc.", nameof(email));
        if (string.IsNullOrWhiteSpace(email.Body))
            throw new ArgumentException("Nội dung email là bắt buộc.", nameof(email));
    }

    private sealed class BrevoEmailRequest
    {
        [JsonPropertyName("sender")] public BrevoContact Sender { get; set; } = null!;
        [JsonPropertyName("to")] public IReadOnlyCollection<BrevoContact> To { get; set; } = [];
        [JsonPropertyName("cc"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyCollection<BrevoContact>? Cc { get; set; }
        [JsonPropertyName("bcc"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyCollection<BrevoContact>? Bcc { get; set; }
        [JsonPropertyName("subject")] public string Subject { get; set; } = null!;
        [JsonPropertyName("htmlContent"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? HtmlContent { get; set; }
        [JsonPropertyName("textContent"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TextContent { get; set; }
        [JsonPropertyName("attachment"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyCollection<BrevoAttachment>? Attachments { get; set; }
    }

    private sealed class BrevoContact
    {
        [JsonPropertyName("email")] public string Email { get; set; } = null!;
        [JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }
    }

    private sealed class BrevoAttachment
    {
        [JsonPropertyName("name")] public string Name { get; set; } = null!;
        [JsonPropertyName("content")] public string Content { get; set; } = null!;
    }
}
