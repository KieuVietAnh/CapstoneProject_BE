using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class MessengerService : IMessengerService
{
    private const string AwaitingTitle = "AwaitingTitle";
    private const string AwaitingDescription = "AwaitingDescription";
    private const string AwaitingLocation = "AwaitingLocation";
    private const string AwaitingArea = "AwaitingArea";
    private const string AwaitingConfirmation = "AwaitingConfirmation";
    private const string Submitting = "Submitting";
    private const string Completed = "Completed";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _uow;
    private readonly IFeedbackService _feedbackService;
    private readonly ILogger<MessengerService> _logger;

    public MessengerService(
        HttpClient httpClient,
        IConfiguration configuration,
        IUnitOfWork uow,
        IFeedbackService feedbackService,
        ILogger<MessengerService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _uow = uow;
        _feedbackService = feedbackService;
        _logger = logger;
    }

    public bool IsVerificationRequestValid(string? mode, string? verifyToken)
    {
        var configuredToken = _configuration["Messenger:VerifyToken"];
        return string.Equals(mode, "subscribe", StringComparison.Ordinal) &&
               SecureEquals(configuredToken, verifyToken);
    }

    public bool IsSignatureValid(string payload, string? signature)
    {
        var appSecret = _configuration["Messenger:AppSecret"];
        if (string.IsNullOrWhiteSpace(appSecret) ||
            string.IsNullOrWhiteSpace(signature) ||
            !signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] suppliedHash;
        try
        {
            suppliedHash = Convert.FromHexString(signature[7..]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return expectedHash.Length == suppliedHash.Length &&
               CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    public async Task ProcessWebhookAsync(
        string payload,
        CancellationToken cancellationToken = default)
    {
        var webhook = JsonSerializer.Deserialize<MessengerWebhookPayload>(payload);
        if (webhook == null || !string.Equals(webhook.Object, "page", StringComparison.Ordinal))
        {
            _logger.LogWarning("Ignored Messenger webhook with unsupported object type.");
            return;
        }

        foreach (var entry in webhook.Entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            foreach (var messagingEvent in entry.Messaging)
            {
                await ProcessEventAsync(entry.Id, messagingEvent, cancellationToken);
            }
        }
    }

    public async Task<MessengerConversationDto?> GetConversationAsync(
        string senderPsid,
        CancellationToken cancellationToken = default)
    {
        var conversation = await Conversations
            .AsNoTracking()
            .Include(c => c.Area)
            .Where(c => c.SenderPsid == senderPsid)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return conversation == null ? null : Map(conversation);
    }

    public async Task<MessengerConversationDto> ResetConversationAsync(
        string senderPsid,
        CancellationToken cancellationToken = default)
    {
        var conversation = await Conversations
            .Include(c => c.Area)
            .Where(c => c.SenderPsid == senderPsid)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Exception("Không tìm thấy hội thoại Messenger.");

        ResetDraft(conversation);
        await _uow.SaveAsync();
        return Map(conversation);
    }

    private IQueryable<MessengerFeedbackConversation> Conversations =>
        _uow.GetRepository<MessengerFeedbackConversation>().Entities;

    private async Task ProcessEventAsync(
        string pageId,
        MessengerMessagingEvent messagingEvent,
        CancellationToken cancellationToken)
    {
        if (messagingEvent.Message?.IsEcho == true ||
            string.IsNullOrWhiteSpace(messagingEvent.Sender?.Id))
        {
            return;
        }

        var senderPsid = messagingEvent.Sender.Id;
        var messageId = messagingEvent.Message?.Mid ??
            $"postback-{senderPsid}-{messagingEvent.Timestamp}";

        var conversation = await Conversations
            .FirstOrDefaultAsync(
                c => c.PageId == pageId && c.SenderPsid == senderPsid,
                cancellationToken);

        if (conversation != null && conversation.LastMessageId == messageId)
        {
            return;
        }

        var text = messagingEvent.Message?.Text ?? messagingEvent.Postback?.Payload;
        if (string.IsNullOrWhiteSpace(text))
        {
            await SendTextAsync(
                senderPsid,
                "Hiện tại bot đang nhận nội dung chữ. Vui lòng mô tả phản ánh bằng tin nhắn chữ; ảnh sẽ được hỗ trợ ở bước tiếp theo.",
                cancellationToken);
            return;
        }

        if (conversation == null)
        {
            conversation = new MessengerFeedbackConversation
            {
                PageId = pageId,
                SenderPsid = senderPsid,
                State = AwaitingTitle,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _uow.GetRepository<MessengerFeedbackConversation>().AddAsync(conversation);
        }

        // Persist the event id first so webhook retries cannot advance the draft twice.
        conversation.LastMessageId = messageId;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        await HandleTextAsync(conversation, text.Trim(), cancellationToken);
    }

    private async Task HandleTextAsync(
        MessengerFeedbackConversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        var command = Normalize(text);
        if (command is "LAM LAI" or "BAT DAU" or "START" or "START_FEEDBACK")
        {
            ResetDraft(conversation);
            await _uow.SaveAsync();
            await SendTextAsync(
                conversation.SenderPsid,
                "Xin chào! Hãy nhập tiêu đề ngắn cho phản ánh của bạn. Ví dụ: Đèn đường bị hỏng.",
                cancellationToken);
            return;
        }

        switch (conversation.State)
        {
            case AwaitingTitle:
                await CaptureTitleAsync(conversation, text, cancellationToken);
                break;
            case AwaitingDescription:
                await CaptureDescriptionAsync(conversation, text, cancellationToken);
                break;
            case AwaitingLocation:
                await CaptureLocationAsync(conversation, text, cancellationToken);
                break;
            case AwaitingArea:
                await CaptureAreaAsync(conversation, text, cancellationToken);
                break;
            case AwaitingConfirmation:
                await ConfirmAsync(conversation, command, cancellationToken);
                break;
            case Submitting:
                await SendTextAsync(
                    conversation.SenderPsid,
                    "Phản ánh đang được hệ thống tiếp nhận. Vui lòng chờ trong giây lát.",
                    cancellationToken);
                break;
            case Completed:
                await SendTextAsync(
                    conversation.SenderPsid,
                    $"Phản ánh gần nhất đã được tạo với mã {conversation.FeedbackId}. Gõ BAT DAU để gửi phản ánh mới.",
                    cancellationToken);
                break;
            default:
                ResetDraft(conversation);
                await _uow.SaveAsync();
                await SendTextAsync(
                    conversation.SenderPsid,
                    "Hội thoại đã được đặt lại. Hãy nhập tiêu đề phản ánh.",
                    cancellationToken);
                break;
        }
    }

    private async Task CaptureTitleAsync(
        MessengerFeedbackConversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        if (text.Length > 200)
        {
            await SendTextAsync(
                conversation.SenderPsid,
                "Tiêu đề tối đa 200 ký tự. Vui lòng nhập ngắn gọn hơn.",
                cancellationToken);
            return;
        }

        conversation.Title = text;
        conversation.State = AwaitingDescription;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
        await SendTextAsync(
            conversation.SenderPsid,
            "Hãy mô tả chi tiết sự việc, mức độ ảnh hưởng và thời điểm bạn phát hiện.",
            cancellationToken);
    }

    private async Task CaptureDescriptionAsync(
        MessengerFeedbackConversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        if (text.Length > 4000)
        {
            await SendTextAsync(
                conversation.SenderPsid,
                "Mô tả tối đa 4.000 ký tự. Vui lòng rút gọn nội dung.",
                cancellationToken);
            return;
        }

        conversation.Description = text;
        conversation.State = AwaitingLocation;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
        await SendTextAsync(
            conversation.SenderPsid,
            "Sự việc xảy ra ở đâu? Vui lòng nhập địa chỉ hoặc mô tả vị trí cụ thể.",
            cancellationToken);
    }

    private async Task CaptureLocationAsync(
        MessengerFeedbackConversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        if (text.Length > 500)
        {
            await SendTextAsync(
                conversation.SenderPsid,
                "Vị trí tối đa 500 ký tự. Vui lòng nhập ngắn gọn hơn.",
                cancellationToken);
            return;
        }

        conversation.LocationText = text;
        conversation.State = AwaitingArea;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
        await SendAreaChoicesAsync(conversation.SenderPsid, cancellationToken);
    }

    private async Task CaptureAreaAsync(
        MessengerFeedbackConversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        var areas = await GetActiveAreasAsync(cancellationToken);
        var selectedArea = ResolveArea(areas, text);
        if (selectedArea == null)
        {
            await SendTextAsync(
                conversation.SenderPsid,
                "Không xác định được khu vực. Hãy nhập đúng mã số hoặc tên khu vực trong danh sách.",
                cancellationToken);
            await SendAreaChoicesAsync(conversation.SenderPsid, cancellationToken);
            return;
        }

        conversation.AreaId = selectedArea.AreaId;
        conversation.State = AwaitingConfirmation;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
        await SendConfirmationAsync(conversation, selectedArea.AreaName, cancellationToken);
    }

    private async Task ConfirmAsync(
        MessengerFeedbackConversation conversation,
        string command,
        CancellationToken cancellationToken)
    {
        if (command is "HUY" or "LAM LAI")
        {
            ResetDraft(conversation);
            await _uow.SaveAsync();
            await SendTextAsync(
                conversation.SenderPsid,
                "Đã hủy nội dung cũ. Hãy nhập tiêu đề phản ánh mới.",
                cancellationToken);
            return;
        }

        if (command is not ("XAC NHAN" or "DONG Y" or "YES" or "OK"))
        {
            await SendTextAsync(
                conversation.SenderPsid,
                "Gõ XAC NHAN để gửi phản ánh hoặc HUY để nhập lại.",
                cancellationToken);
            return;
        }

        if (conversation.AreaId == null ||
            string.IsNullOrWhiteSpace(conversation.Title) ||
            string.IsNullOrWhiteSpace(conversation.Description) ||
            string.IsNullOrWhiteSpace(conversation.LocationText))
        {
            ResetDraft(conversation);
            await _uow.SaveAsync();
            await SendTextAsync(
                conversation.SenderPsid,
                "Nội dung chưa đầy đủ nên hội thoại đã được đặt lại. Hãy nhập tiêu đề phản ánh.",
                cancellationToken);
            return;
        }

        conversation.State = Submitting;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        var submissionUserId = await GetSubmissionUserIdAsync(cancellationToken);
        var feedback = await _feedbackService.CreateAsync(
            submissionUserId,
            new FeedbackCreateRequest
            {
                AreaId = conversation.AreaId.Value,
                Title = conversation.Title,
                Description = conversation.Description,
                LocationText = conversation.LocationText,
                GeoSource = "Messenger"
            },
            []);

        conversation.FeedbackId = feedback.FeedbackId;
        conversation.State = Completed;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        await SendTextAsync(
            conversation.SenderPsid,
            $"Phản ánh đã được tiếp nhận thành công. Mã phản ánh: {feedback.FeedbackId}. Gõ BAT DAU để gửi phản ánh mới.",
            cancellationToken);
    }

    private async Task SendAreaChoicesAsync(string senderPsid, CancellationToken cancellationToken)
    {
        var areas = await GetActiveAreasAsync(cancellationToken);
        if (areas.Count == 0)
        {
            await SendTextAsync(
                senderPsid,
                "Hệ thống chưa cấu hình khu vực tiếp nhận. Vui lòng liên hệ quản trị viên.",
                cancellationToken);
            return;
        }

        var lines = areas
            .Take(25)
            .Select(a => $"{a.AreaId} - {a.AreaName}");
        var suffix = areas.Count > 25
            ? "\nBạn cũng có thể nhập chính xác tên khu vực nếu không thấy trong danh sách."
            : string.Empty;

        await SendTextAsync(
            senderPsid,
            $"Chọn khu vực bằng cách nhập mã số hoặc tên:\n{string.Join("\n", lines)}{suffix}",
            cancellationToken);
    }

    private async Task SendConfirmationAsync(
        MessengerFeedbackConversation conversation,
        string selectedAreaName,
        CancellationToken cancellationToken)
    {
        var summary = $"Vui lòng kiểm tra:\n" +
                      $"Tiêu đề: {conversation.Title}\n" +
                      $"Mô tả: {conversation.Description}\n" +
                      $"Vị trí: {conversation.LocationText}\n" +
                      $"Khu vực: {selectedAreaName}\n\n" +
                      "Gõ XAC NHAN để gửi hoặc HUY để nhập lại.";
        await SendTextAsync(conversation.SenderPsid, summary, cancellationToken);
    }

    private async Task SendTextAsync(
        string senderPsid,
        string text,
        CancellationToken cancellationToken)
    {
        var accessToken = GetRequiredConfiguration("Messenger:PageAccessToken");
        var graphVersion = _configuration["Messenger:GraphApiVersion"] ?? "v25.0";
        var endpoint = $"https://graph.facebook.com/{graphVersion}/me/messages";

        foreach (var chunk in SplitMessage(text, 1800))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new
                {
                    recipient = new { id = senderPsid },
                    messaging_type = "RESPONSE",
                    message = new { text = chunk }
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Messenger Send API failed with {StatusCode}: {ResponseBody}",
                    (int)response.StatusCode,
                    responseBody);
                response.EnsureSuccessStatusCode();
            }
        }
    }

    private static IEnumerable<string> SplitMessage(string message, int maximumLength)
    {
        for (var offset = 0; offset < message.Length; offset += maximumLength)
        {
            var length = Math.Min(maximumLength, message.Length - offset);
            yield return message.Substring(offset, length);
        }
    }

    private async Task<List<OperatingArea>> GetActiveAreasAsync(CancellationToken cancellationToken)
    {
        return await _uow.GetRepository<OperatingArea>().Entities
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.AreaName)
            .ToListAsync(cancellationToken);
    }

    private static OperatingArea? ResolveArea(IReadOnlyCollection<OperatingArea> areas, string input)
    {
        if (int.TryParse(input, out var areaId))
        {
            return areas.FirstOrDefault(a => a.AreaId == areaId);
        }

        var normalizedInput = Normalize(input);
        var exact = areas.FirstOrDefault(a => Normalize(a.AreaName) == normalizedInput);
        if (exact != null)
        {
            return exact;
        }

        var matches = areas
            .Where(a => Normalize(a.AreaName).Contains(normalizedInput, StringComparison.Ordinal))
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<Guid> GetSubmissionUserIdAsync(CancellationToken cancellationToken)
    {
        var value = GetRequiredConfiguration("Messenger:SubmissionUserId");
        if (!Guid.TryParse(value, out var userId))
        {
            throw new InvalidOperationException(
                "Messenger:SubmissionUserId must be a valid SERVICEUSER id.");
        }

        var isValidSubmissionUser = await _uow.GetRepository<User>().Entities
            .AsNoTracking()
            .AnyAsync(
                user => user.UserId == userId &&
                        user.IsActive &&
                        user.Role.RoleName == UserRole.SERVICEUSER,
                cancellationToken);

        return isValidSubmissionUser
            ? userId
            : throw new InvalidOperationException(
                "Messenger:SubmissionUserId must reference an active SERVICEUSER account.");
    }

    private string GetRequiredConfiguration(string key)
    {
        return _configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing configuration: {key}");
    }

    private static void ResetDraft(MessengerFeedbackConversation conversation)
    {
        conversation.State = AwaitingTitle;
        conversation.Title = null;
        conversation.Description = null;
        conversation.LocationText = null;
        conversation.AreaId = null;
        conversation.Area = null;
        conversation.FeedbackId = null;
        conversation.UpdatedAt = DateTime.UtcNow;
    }

    private static MessengerConversationDto Map(MessengerFeedbackConversation conversation)
    {
        return new MessengerConversationDto
        {
            ConversationId = conversation.ConversationId,
            PageId = conversation.PageId,
            SenderPsid = conversation.SenderPsid,
            State = conversation.State,
            Title = conversation.Title,
            Description = conversation.Description,
            LocationText = conversation.LocationText,
            AreaId = conversation.AreaId,
            AreaName = conversation.Area?.AreaName,
            FeedbackId = conversation.FeedbackId,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt
        };
    }

    private static bool SecureEquals(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return false;
        }

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
