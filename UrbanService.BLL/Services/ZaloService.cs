using System.Globalization;
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

public class ZaloService : IZaloService
{
    private const string Idle = "Idle";
    private const string AwaitingTitle = "AwaitingTitle";
    private const string AwaitingDescription = "AwaitingDescription";
    private const string AwaitingLocation = "AwaitingLocation";
    private const string AwaitingArea = "AwaitingArea";
    private const string AwaitingConfirmation = "AwaitingConfirmation";
    private const string Submitting = "Submitting";
    private const string Completed = "Completed";
    private const int FeedbackHistoryPageSize = 5;
    private const int MaximumOutgoingMessageLength = 1900;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _uow;
    private readonly IFeedbackService _feedbackService;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IZaloAccessTokenProvider _accessTokenProvider;
    private readonly ILogger<ZaloService> _logger;

    public ZaloService(
        HttpClient httpClient,
        IConfiguration configuration,
        IUnitOfWork uow,
        IFeedbackService feedbackService,
        ICloudinaryService cloudinaryService,
        IZaloAccessTokenProvider accessTokenProvider,
        ILogger<ZaloService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _uow = uow;
        _feedbackService = feedbackService;
        _cloudinaryService = cloudinaryService;
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;
    }

    public bool IsSignatureValid(string payload, string? signature)
    {
        var configuredAppId = _configuration["Zalo:AppId"];
        var secretKey = _configuration["Zalo:OaSecretKey"];
        if (string.IsNullOrWhiteSpace(configuredAppId) ||
            string.IsNullOrWhiteSpace(secretKey) ||
            string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        string? payloadAppId;
        string? timestamp;
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            payloadAppId = root.TryGetProperty("app_id", out var appIdElement)
                ? appIdElement.ToString()
                : null;
            timestamp = root.TryGetProperty("timestamp", out var timestampElement)
                ? timestampElement.ToString()
                : null;
        }
        catch (JsonException)
        {
            return false;
        }

        if (!SecureEquals(configuredAppId, payloadAppId) || string.IsNullOrWhiteSpace(timestamp))
        {
            return false;
        }

        var suppliedSignature = signature.Trim();
        if (suppliedSignature.StartsWith("mac=", StringComparison.OrdinalIgnoreCase))
        {
            suppliedSignature = suppliedSignature[4..];
        }
        else if (suppliedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            suppliedSignature = suppliedSignature[7..];
        }

        byte[] suppliedHash;
        try
        {
            suppliedHash = Convert.FromHexString(suppliedSignature);
        }
        catch (FormatException)
        {
            return false;
        }

        var signedValue = $"{configuredAppId}{payload}{timestamp}{secretKey}";
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(signedValue));
        return expectedHash.Length == suppliedHash.Length &&
               CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    public async Task ProcessWebhookAsync(
        string payload,
        CancellationToken cancellationToken = default)
    {
        ZaloWebhookPayload? webhook;
        try
        {
            webhook = JsonSerializer.Deserialize<ZaloWebhookPayload>(payload);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Ignored malformed Zalo webhook payload.");
            return;
        }

        if (webhook == null ||
            string.IsNullOrWhiteSpace(webhook.Sender?.Id) ||
            string.IsNullOrWhiteSpace(webhook.Recipient?.Id) ||
            string.IsNullOrWhiteSpace(webhook.EventName))
        {
            _logger.LogWarning("Ignored incomplete Zalo webhook payload.");
            return;
        }

        var configuredOaId = _configuration["Zalo:OaId"];
        if (!string.IsNullOrWhiteSpace(configuredOaId) &&
            !SecureEquals(configuredOaId, webhook.Recipient.Id))
        {
            _logger.LogWarning("Ignored Zalo webhook for an unexpected OA.");
            return;
        }

        await ProcessEventAsync(webhook, cancellationToken);
    }

    public async Task<ZaloConversationDto?> GetConversationAsync(
        string senderUserId,
        CancellationToken cancellationToken = default)
    {
        var oaId = GetRequiredConfiguration("Zalo:OaId");
        var conversation = await Conversations
            .AsNoTracking()
            .Include(item => item.Area)
            .FirstOrDefaultAsync(
                item => item.OaId == oaId && item.SenderUserId == senderUserId,
                cancellationToken);
        return conversation == null ? null : Map(conversation);
    }

    public async Task<ZaloConversationDto> ResetConversationAsync(
        string senderUserId,
        CancellationToken cancellationToken = default)
    {
        var oaId = GetRequiredConfiguration("Zalo:OaId");
        var conversation = await Conversations
            .Include(item => item.Area)
            .FirstOrDefaultAsync(
                item => item.OaId == oaId && item.SenderUserId == senderUserId,
                cancellationToken)
            ?? throw new Exception("Không tìm thấy hội thoại Zalo.");

        await ResetDraftAsync(conversation, setIdle: false, cancellationToken);
        await _uow.SaveAsync();
        return Map(conversation);
    }

    private IQueryable<ZaloFeedbackConversation> Conversations =>
        _uow.GetRepository<ZaloFeedbackConversation>().Entities;

    private async Task ProcessEventAsync(
        ZaloWebhookPayload webhook,
        CancellationToken cancellationToken)
    {
        var oaId = webhook.Recipient!.Id!;
        var senderUserId = webhook.Sender!.Id!;
        var messageId = webhook.Message?.MessageId ??
            $"{webhook.EventName}-{senderUserId}-{webhook.Timestamp}";
        var conversation = await Conversations.FirstOrDefaultAsync(
            item => item.OaId == oaId && item.SenderUserId == senderUserId,
            cancellationToken);

        if (conversation != null && conversation.LastMessageId == messageId)
        {
            if (conversation.State == Completed && conversation.FeedbackId.HasValue)
            {
                await SendMainMenuAsync(
                    senderUserId,
                    $"Phản ánh đã được tiếp nhận thành công. Mã phản ánh: {conversation.FeedbackId}.",
                    cancellationToken);
                return;
            }

            if (conversation.State != Submitting)
            {
                return;
            }

            conversation.State = AwaitingConfirmation;
            conversation.LastMessageId = null;
            conversation.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveAsync();
        }

        if (conversation == null)
        {
            conversation = new ZaloFeedbackConversation
            {
                OaId = oaId,
                SenderUserId = senderUserId,
                State = Idle,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _uow.GetRepository<ZaloFeedbackConversation>().AddAsync(conversation);
        }

        conversation.LastMessageId = messageId;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        switch (webhook.EventName)
        {
            case "user_send_text" when !string.IsNullOrWhiteSpace(webhook.Message?.Text):
                await HandleTextAsync(conversation, webhook.Message.Text.Trim(), cancellationToken);
                break;
            case "user_send_location":
                await HandleLocationAsync(conversation, webhook.Message, cancellationToken);
                break;
            case "user_send_image":
                await HandleImagesAsync(conversation, webhook.Message, cancellationToken);
                break;
            default:
                await SendMainMenuAsync(
                    senderUserId,
                    "Vui lòng gửi nội dung bằng chữ hoặc chia sẻ vị trí từ Zalo.",
                    cancellationToken);
                break;
        }
    }

    private async Task HandleTextAsync(
        ZaloFeedbackConversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        var command = Normalize(text);
        if (command is "LAM LAI" or "BAT DAU" or "START" or "GUI PHAN ANH")
        {
            await ResetDraftAsync(conversation, setIdle: false, cancellationToken);
            await _uow.SaveAsync();
            await SendDraftPromptAsync(
                conversation.SenderUserId,
                "Xin chào! Hãy nhập tiêu đề ngắn cho phản ánh của bạn. Ví dụ: Đèn đường bị hỏng.",
                cancellationToken);
            return;
        }

        if (TryGetFeedbackHistoryPage(command, out var pageNumber))
        {
            await SendFeedbackHistoryAsync(conversation, pageNumber, cancellationToken);
            return;
        }

        if (command is "HELP" or "TRO GIUP" or "HUONG DAN")
        {
            await SendHelpAsync(conversation.SenderUserId, cancellationToken);
            return;
        }

        if (command is "MENU" or "MAIN MENU")
        {
            await ResetDraftAsync(conversation, setIdle: true, cancellationToken);
            await _uow.SaveAsync();
            await SendMainMenuAsync(
                conversation.SenderUserId,
                "Bạn muốn thực hiện thao tác nào?",
                cancellationToken);
            return;
        }

        if (command is "HUY" or "CANCEL")
        {
            await ResetDraftAsync(conversation, setIdle: true, cancellationToken);
            await _uow.SaveAsync();
            await SendMainMenuAsync(
                conversation.SenderUserId,
                "Đã hủy nội dung đang nhập.",
                cancellationToken);
            return;
        }

        if (conversation.State is Idle or Completed)
        {
            if (command == "1")
            {
                await ResetDraftAsync(conversation, setIdle: false, cancellationToken);
                await _uow.SaveAsync();
                await SendDraftPromptAsync(
                    conversation.SenderUserId,
                    "Hãy nhập tiêu đề ngắn cho phản ánh của bạn.",
                    cancellationToken);
                return;
            }

            if (command == "2")
            {
                await SendFeedbackHistoryAsync(conversation, 1, cancellationToken);
                return;
            }

            if (command == "3")
            {
                await SendHelpAsync(conversation.SenderUserId, cancellationToken);
                return;
            }
        }

        switch (conversation.State)
        {
            case Idle:
                await SendMainMenuAsync(
                    conversation.SenderUserId,
                    "Xin chào! Bạn muốn thực hiện thao tác nào?",
                    cancellationToken);
                break;
            case AwaitingTitle:
                await CaptureTitleAsync(conversation, text, cancellationToken);
                break;
            case AwaitingDescription:
                await CaptureDescriptionAsync(conversation, text, cancellationToken);
                break;
            case AwaitingLocation:
                await CaptureLocationTextAsync(conversation, text, cancellationToken);
                break;
            case AwaitingArea:
                await CaptureAreaAsync(conversation, text, cancellationToken);
                break;
            case AwaitingConfirmation:
                await ConfirmAsync(conversation, command, cancellationToken);
                break;
            case Submitting:
                await SendTextAsync(
                    conversation.SenderUserId,
                    "Phản ánh đang được hệ thống tiếp nhận. Vui lòng chờ trong giây lát.",
                    cancellationToken);
                break;
            case Completed:
                await SendMainMenuAsync(
                    conversation.SenderUserId,
                    $"Phản ánh gần nhất đã được tạo với mã {conversation.FeedbackId}.",
                    cancellationToken);
                break;
            default:
                await ResetDraftAsync(conversation, setIdle: true, cancellationToken);
                await _uow.SaveAsync();
                await SendMainMenuAsync(
                    conversation.SenderUserId,
                    "Hội thoại đã được đặt lại. Bạn muốn thực hiện thao tác nào?",
                    cancellationToken);
                break;
        }
    }

    private async Task CaptureTitleAsync(
        ZaloFeedbackConversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        if (text.Length > 200)
        {
            await SendDraftPromptAsync(
                conversation.SenderUserId,
                "Tiêu đề tối đa 200 ký tự. Vui lòng nhập ngắn gọn hơn.",
                cancellationToken);
            return;
        }

        conversation.Title = text;
        conversation.State = AwaitingDescription;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
        await SendDraftPromptAsync(
            conversation.SenderUserId,
            "Hãy mô tả chi tiết sự việc, mức độ ảnh hưởng và thời điểm bạn phát hiện.",
            cancellationToken);
    }

    private async Task CaptureDescriptionAsync(
        ZaloFeedbackConversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        if (text.Length > 4000)
        {
            await SendDraftPromptAsync(
                conversation.SenderUserId,
                "Mô tả tối đa 4.000 ký tự. Vui lòng rút gọn nội dung.",
                cancellationToken);
            return;
        }

        conversation.Description = text;
        conversation.State = AwaitingLocation;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
        await SendDraftPromptAsync(
            conversation.SenderUserId,
            "Sự việc xảy ra ở đâu? Hãy nhập địa chỉ hoặc dùng chức năng Chia sẻ vị trí của Zalo.",
            cancellationToken);
    }

    private async Task CaptureLocationTextAsync(
        ZaloFeedbackConversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        if (text.Length > 500)
        {
            await SendDraftPromptAsync(
                conversation.SenderUserId,
                "Vị trí tối đa 500 ký tự. Vui lòng nhập ngắn gọn hơn.",
                cancellationToken);
            return;
        }

        conversation.LocationText = text;
        conversation.Latitude = null;
        conversation.Longitude = null;
        conversation.State = AwaitingArea;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
        await SendAreaChoicesAsync(conversation.SenderUserId, cancellationToken);
    }

    private async Task HandleLocationAsync(
        ZaloFeedbackConversation conversation,
        ZaloIncomingMessage? message,
        CancellationToken cancellationToken)
    {
        if (conversation.State != AwaitingLocation)
        {
            await SendDraftPromptAsync(
                conversation.SenderUserId,
                "Hãy bắt đầu phản ánh và chia sẻ vị trí khi hệ thống yêu cầu.",
                cancellationToken);
            return;
        }

        var coordinates = message?.Attachments
            .FirstOrDefault(item => string.Equals(item.Type, "location", StringComparison.OrdinalIgnoreCase))?
            .Payload?
            .Coordinates;
        if (!TryParseCoordinate(coordinates?.Latitude, -90, 90, out var latitude) ||
            !TryParseCoordinate(coordinates?.Longitude, -180, 180, out var longitude))
        {
            await SendDraftPromptAsync(
                conversation.SenderUserId,
                "Không đọc được vị trí đã chia sẻ. Vui lòng thử lại hoặc nhập địa chỉ bằng chữ.",
                cancellationToken);
            return;
        }

        conversation.Latitude = latitude;
        conversation.Longitude = longitude;
        conversation.LocationText = $"Vị trí chia sẻ từ Zalo ({latitude}, {longitude})";
        conversation.State = AwaitingArea;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
        await SendAreaChoicesAsync(conversation.SenderUserId, cancellationToken);
    }

    private async Task HandleImagesAsync(
        ZaloFeedbackConversation conversation,
        ZaloIncomingMessage? message,
        CancellationToken cancellationToken)
    {
        if (conversation.State is Idle or Completed or Submitting)
        {
            await SendMainMenuAsync(
                conversation.SenderUserId,
                "Hãy bắt đầu phản ánh trước khi gửi ảnh minh chứng.",
                cancellationToken);
            return;
        }

        var maximumAttachments = GetPositiveConfiguration("Zalo:MaxImagesPerFeedback", 5);
        var existingCount = await _uow.GetRepository<ZaloFeedbackDraftAttachment>().Entities
            .CountAsync(item => item.ConversationId == conversation.ConversationId, cancellationToken);
        var availableSlots = Math.Max(0, maximumAttachments - existingCount);
        if (availableSlots == 0)
        {
            await SendDraftPromptAsync(
                conversation.SenderUserId,
                $"Mỗi phản ánh được đính kèm tối đa {maximumAttachments} ảnh.",
                cancellationToken);
            return;
        }

        var imageUrls = message?.Attachments
            .Where(item => string.Equals(item.Type, "image", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Payload?.Url)
            .Where(url => IsAllowedSourceUrl(url))
            .Distinct(StringComparer.Ordinal)
            .Take(availableSlots)
            .Cast<string>()
            .ToList() ?? [];
        if (imageUrls.Count == 0)
        {
            await SendDraftPromptAsync(
                conversation.SenderUserId,
                "Không đọc được ảnh đã gửi. Vui lòng thử lại với một ảnh khác.",
                cancellationToken);
            return;
        }

        var now = DateTime.UtcNow;
        await _uow.GetRepository<ZaloFeedbackDraftAttachment>().AddRangeAsync(
            imageUrls.Select(url => new ZaloFeedbackDraftAttachment
            {
                ConversationId = conversation.ConversationId,
                SourceUrl = url,
                FileType = "image",
                CreatedAt = now
            }));
        conversation.UpdatedAt = now;
        await _uow.SaveAsync();

        await SendDraftPromptAsync(
            conversation.SenderUserId,
            $"Đã thêm {imageUrls.Count} ảnh minh chứng. Hãy tiếp tục nội dung đang nhập.",
            cancellationToken);
    }

    private async Task CaptureAreaAsync(
        ZaloFeedbackConversation conversation,
        string text,
        CancellationToken cancellationToken)
    {
        var areas = await GetActiveAreasAsync(cancellationToken);
        var selectedArea = ResolveArea(areas, text);
        if (selectedArea == null)
        {
            await SendTextAsync(
                conversation.SenderUserId,
                "Không xác định được khu vực. Hãy nhập đúng mã số hoặc tên khu vực trong danh sách.",
                cancellationToken);
            await SendAreaChoicesAsync(conversation.SenderUserId, cancellationToken);
            return;
        }

        conversation.AreaId = selectedArea.AreaId;
        conversation.State = AwaitingConfirmation;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
        await SendConfirmationAsync(conversation, selectedArea.AreaName, cancellationToken);
    }

    private async Task ConfirmAsync(
        ZaloFeedbackConversation conversation,
        string command,
        CancellationToken cancellationToken)
    {
        if (command is not ("XAC NHAN" or "DONG Y" or "YES" or "OK"))
        {
            await SendDraftPromptAsync(
                conversation.SenderUserId,
                "Gõ XÁC NHẬN để gửi phản ánh hoặc LÀM LẠI để bắt đầu lại.",
                cancellationToken);
            return;
        }

        if (conversation.AreaId == null ||
            string.IsNullOrWhiteSpace(conversation.Title) ||
            string.IsNullOrWhiteSpace(conversation.Description) ||
            string.IsNullOrWhiteSpace(conversation.LocationText))
        {
            await ResetDraftAsync(conversation, setIdle: false, cancellationToken);
            await _uow.SaveAsync();
            await SendDraftPromptAsync(
                conversation.SenderUserId,
                "Nội dung chưa đầy đủ nên hội thoại đã được đặt lại. Hãy nhập tiêu đề phản ánh.",
                cancellationToken);
            return;
        }

        conversation.State = Submitting;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        FeedbackDetailDto? feedback = null;
        try
        {
            var submissionUserId = await GetSubmissionUserIdAsync(cancellationToken);
            var draftAttachments = await _uow.GetRepository<ZaloFeedbackDraftAttachment>().Entities
                .Where(item => item.ConversationId == conversation.ConversationId)
                .OrderBy(item => item.CreatedAt)
                .ToListAsync(cancellationToken);
            var uploadedAttachments = await UploadDraftAttachmentsAsync(
                draftAttachments,
                cancellationToken);
            feedback = await _feedbackService.CreateAsync(
                submissionUserId,
                new FeedbackCreateRequest
                {
                    AreaId = conversation.AreaId.Value,
                    Title = conversation.Title,
                    Description = conversation.Description,
                    LocationText = conversation.LocationText,
                    Latitude = conversation.Latitude,
                    Longitude = conversation.Longitude,
                    GeoSource = "Zalo",
                    SubmissionChannel = FeedbackSubmissionChannel.Zalo
                },
                uploadedAttachments);

            conversation.FeedbackId = feedback.FeedbackId;
            conversation.State = Completed;
            conversation.UpdatedAt = DateTime.UtcNow;

            await _uow.GetRepository<ZaloFeedbackSubmission>().AddAsync(
                new ZaloFeedbackSubmission
                {
                    ConversationId = conversation.ConversationId,
                    FeedbackId = feedback.FeedbackId,
                    CreatedAt = DateTime.UtcNow
                });

            if (draftAttachments.Count > 0)
            {
                _uow.GetRepository<ZaloFeedbackDraftAttachment>().DeleteRange(draftAttachments);
            }
            await _uow.SaveAsync();
        }
        catch
        {
            if (feedback == null)
            {
                conversation.State = AwaitingConfirmation;
                conversation.LastMessageId = null;
            }
            else
            {
                conversation.FeedbackId = feedback.FeedbackId;
                conversation.State = Completed;
            }

            conversation.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _uow.SaveAsync();
            }
            catch (Exception recoveryException)
            {
                _logger.LogError(
                    recoveryException,
                    "Failed to persist Zalo conversation recovery state.");
            }

            throw;
        }

        await SendMainMenuAsync(
            conversation.SenderUserId,
            $"Phản ánh đã được tiếp nhận thành công. Mã phản ánh: {feedback.FeedbackId}.",
            cancellationToken);
    }

    private async Task SendAreaChoicesAsync(
        string senderUserId,
        CancellationToken cancellationToken)
    {
        var areas = await GetActiveAreasAsync(cancellationToken);
        if (areas.Count == 0)
        {
            await SendDraftPromptAsync(
                senderUserId,
                "Hệ thống chưa cấu hình khu vực tiếp nhận. Vui lòng liên hệ quản trị viên.",
                cancellationToken);
            return;
        }

        var lines = areas.Take(25).Select(area => $"{area.AreaId} - {area.AreaName}");
        var suffix = areas.Count > 25
            ? "\nBạn cũng có thể nhập chính xác tên khu vực nếu không thấy trong danh sách."
            : string.Empty;
        await SendDraftPromptAsync(
            senderUserId,
            $"Chọn khu vực bằng cách nhập mã số hoặc tên:\n{string.Join("\n", lines)}{suffix}",
            cancellationToken);
    }

    private async Task SendConfirmationAsync(
        ZaloFeedbackConversation conversation,
        string selectedAreaName,
        CancellationToken cancellationToken)
    {
        var attachmentCount = await _uow.GetRepository<ZaloFeedbackDraftAttachment>().Entities
            .CountAsync(item => item.ConversationId == conversation.ConversationId, cancellationToken);
        var summary = $"Vui lòng kiểm tra:\n" +
                      $"Tiêu đề: {conversation.Title}\n" +
                      $"Mô tả: {conversation.Description}\n" +
                      $"Vị trí: {conversation.LocationText}\n" +
                      $"Khu vực: {selectedAreaName}\n\n" +
                      $"Ảnh minh chứng: {attachmentCount}\n\n" +
                      "Gõ XÁC NHẬN để gửi hoặc LÀM LẠI để bắt đầu lại.";
        await SendDraftPromptAsync(conversation.SenderUserId, summary, cancellationToken);
    }

    private Task SendDraftPromptAsync(
        string senderUserId,
        string text,
        CancellationToken cancellationToken)
    {
        return SendTextAsync(senderUserId, $"{text}\n\nGõ HỦY để dừng.", cancellationToken);
    }

    private Task SendMainMenuAsync(
        string senderUserId,
        string text,
        CancellationToken cancellationToken)
    {
        return SendTextAsync(
            senderUserId,
            $"{text}\n\n1. Gửi phản ánh\n2. Phản ánh đã gửi\n3. Trợ giúp",
            cancellationToken);
    }

    private Task SendHelpAsync(string senderUserId, CancellationToken cancellationToken)
    {
        const string helpText =
            "Bạn có thể gửi phản ánh mới hoặc xem lại các phản ánh đã gửi từ Zalo. " +
            "Khi tạo phản ánh, hệ thống sẽ lần lượt hỏi tiêu đề, mô tả, vị trí, khu vực và yêu cầu xác nhận.";
        return SendMainMenuAsync(senderUserId, helpText, cancellationToken);
    }

    private async Task SendFeedbackHistoryAsync(
        ZaloFeedbackConversation conversation,
        int requestedPage,
        CancellationToken cancellationToken)
    {
        var submissions = _uow.GetRepository<ZaloFeedbackSubmission>().Entities
            .AsNoTracking()
            .Where(item => item.ConversationId == conversation.ConversationId);
        var totalItems = await submissions.CountAsync(cancellationToken);
        if (totalItems == 0)
        {
            await SendMainMenuAsync(
                conversation.SenderUserId,
                "Bạn chưa gửi phản ánh nào từ Zalo.",
                cancellationToken);
            return;
        }

        var totalPages = (int)Math.Ceiling(totalItems / (double)FeedbackHistoryPageSize);
        var pageNumber = Math.Clamp(requestedPage, 1, totalPages);
        var items = await submissions
            .OrderByDescending(item => item.CreatedAt)
            .Skip((pageNumber - 1) * FeedbackHistoryPageSize)
            .Take(FeedbackHistoryPageSize)
            .Select(item => new
            {
                item.Feedback.FeedbackId,
                item.Feedback.Title,
                item.Feedback.Status,
                item.Feedback.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var message = new StringBuilder($"Phản ánh đã gửi - trang {pageNumber}/{totalPages}:\n");
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var number = (pageNumber - 1) * FeedbackHistoryPageSize + index + 1;
            message.AppendLine();
            message.AppendLine($"{number}. {item.Title}");
            message.AppendLine($"Mã: {item.FeedbackId}");
            message.AppendLine($"Trạng thái: {GetStatusLabel(item.Status)}");
            message.AppendLine($"Gửi lúc: {item.CreatedAt:dd/MM/yyyy HH:mm}");
        }

        if (pageNumber < totalPages)
        {
            message.AppendLine($"\nGõ TRANG {pageNumber + 1} để xem tiếp.");
        }

        if (pageNumber > 1)
        {
            message.AppendLine($"Gõ TRANG {pageNumber - 1} để quay lại.");
        }

        message.AppendLine("Gõ MENU để trở về menu chính.");
        await SendTextAsync(conversation.SenderUserId, message.ToString().TrimEnd(), cancellationToken);
    }

    private async Task SendTextAsync(
        string senderUserId,
        string text,
        CancellationToken cancellationToken)
    {
        var accessToken = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken);
        foreach (var chunk in SplitMessage(text, MaximumOutgoingMessageLength))
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://openapi.zalo.me/v3.0/oa/message/cs")
            {
                Content = JsonContent.Create(new
                {
                    recipient = new { user_id = senderUserId },
                    message = new { text = chunk }
                })
            };
            request.Headers.TryAddWithoutValidation("access_token", accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode || HasZaloApiError(responseBody))
            {
                _logger.LogError(
                    "Zalo Send API failed with HTTP {StatusCode}.",
                    (int)response.StatusCode);
                throw new HttpRequestException("Zalo Send API rejected the outgoing message.");
            }
        }
    }

    private static bool HasZaloApiError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("error", out var error) &&
                   error.TryGetInt32(out var errorCode) &&
                   errorCode != 0;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private async Task<List<OperatingArea>> GetActiveAreasAsync(CancellationToken cancellationToken)
    {
        return await _uow.GetRepository<OperatingArea>().Entities
            .AsNoTracking()
            .Where(area => area.IsActive)
            .OrderBy(area => area.AreaName)
            .ToListAsync(cancellationToken);
    }

    private static OperatingArea? ResolveArea(
        IReadOnlyCollection<OperatingArea> areas,
        string input)
    {
        if (int.TryParse(input, out var areaId))
        {
            return areas.FirstOrDefault(area => area.AreaId == areaId);
        }

        var normalizedInput = Normalize(input);
        var exact = areas.FirstOrDefault(area => Normalize(area.AreaName) == normalizedInput);
        if (exact != null)
        {
            return exact;
        }

        var matches = areas
            .Where(area => Normalize(area.AreaName).Contains(normalizedInput, StringComparison.Ordinal))
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<IReadOnlyCollection<UploadedFeedbackAttachmentDto>> UploadDraftAttachmentsAsync(
        IReadOnlyCollection<ZaloFeedbackDraftAttachment> draftAttachments,
        CancellationToken cancellationToken)
    {
        var maximumBytes = GetPositiveConfiguration("Zalo:MaxImageBytes", 5 * 1024 * 1024);
        var uploadedAttachments = new List<UploadedFeedbackAttachmentDto>(draftAttachments.Count);

        foreach (var draftAttachment in draftAttachments)
        {
            if (!IsAllowedSourceUrl(draftAttachment.SourceUrl) ||
                !Uri.TryCreate(draftAttachment.SourceUrl, UriKind.Absolute, out var sourceUri))
            {
                throw new InvalidOperationException("Zalo returned an unsupported image URL.");
            }

            using var response = await _httpClient.GetAsync(
                sourceUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
            {
                throw new InvalidOperationException("Zalo attachment is not a supported image.");
            }

            if (response.Content.Headers.ContentLength > maximumBytes)
            {
                throw new InvalidOperationException(
                    $"Zalo image exceeds the {maximumBytes}-byte limit.");
            }

            await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var uploadStream = new MemoryStream();
            await CopyWithLimitAsync(sourceStream, uploadStream, maximumBytes, cancellationToken);
            uploadStream.Position = 0;

            var extension = contentType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".jpg"
            };
            var upload = await _cloudinaryService.UploadAsync(
                uploadStream,
                $"zalo-{Guid.NewGuid():N}{extension}",
                contentType,
                "urban-service/zalo-feedbacks",
                cancellationToken);
            uploadedAttachments.Add(new UploadedFeedbackAttachmentDto
            {
                FileUrl = upload.FileUrl,
                FileType = upload.FileType ?? contentType
            });
        }

        return uploadedAttachments;
    }

    private static async Task CopyWithLimitAsync(
        Stream source,
        Stream destination,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        var totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > maximumBytes)
            {
                throw new InvalidOperationException(
                    $"Zalo image exceeds the {maximumBytes}-byte limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private bool IsAllowedSourceUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var configuredHosts = _configuration["Zalo:AllowedMediaHostSuffixes"] ??
            "zdn.vn,zadn.vn,zalo.me,zaloapp.com";
        return configuredHosts
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(suffix =>
                uri.Host.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith($".{suffix}", StringComparison.OrdinalIgnoreCase));
    }

    private int GetPositiveConfiguration(string key, int defaultValue)
    {
        return int.TryParse(_configuration[key], out var configuredValue) && configuredValue > 0
            ? configuredValue
            : defaultValue;
    }

    private async Task<Guid> GetSubmissionUserIdAsync(CancellationToken cancellationToken)
    {
        var value = GetRequiredConfiguration("Zalo:SubmissionUserId");
        if (!Guid.TryParse(value, out var userId))
        {
            throw new InvalidOperationException(
                "Zalo:SubmissionUserId must be a valid SERVICEUSER id.");
        }

        var isValidSubmissionUser = await _uow.GetRepository<User>().Entities
            .AsNoTracking()
            .AnyAsync(
                user => user.UserId == userId &&
                        user.IsActive &&
                        user.Role.RoleName.ToUpper() == UserRole.SERVICEUSER,
                cancellationToken);
        return isValidSubmissionUser
            ? userId
            : throw new InvalidOperationException(
                "Zalo:SubmissionUserId must reference an active SERVICEUSER account.");
    }

    private string GetRequiredConfiguration(string key)
    {
        return _configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Missing configuration: {key}");
    }

    private async Task ResetDraftAsync(
        ZaloFeedbackConversation conversation,
        bool setIdle,
        CancellationToken cancellationToken)
    {
        var draftAttachments = await _uow.GetRepository<ZaloFeedbackDraftAttachment>().Entities
            .Where(item => item.ConversationId == conversation.ConversationId)
            .ToListAsync(cancellationToken);
        if (draftAttachments.Count > 0)
        {
            _uow.GetRepository<ZaloFeedbackDraftAttachment>().DeleteRange(draftAttachments);
        }

        conversation.State = setIdle ? Idle : AwaitingTitle;
        conversation.Title = null;
        conversation.Description = null;
        conversation.LocationText = null;
        conversation.Latitude = null;
        conversation.Longitude = null;
        conversation.AreaId = null;
        conversation.Area = null;
        conversation.FeedbackId = null;
        conversation.UpdatedAt = DateTime.UtcNow;
    }

    private static ZaloConversationDto Map(ZaloFeedbackConversation conversation)
    {
        return new ZaloConversationDto
        {
            ConversationId = conversation.ConversationId,
            OaId = conversation.OaId,
            SenderUserId = conversation.SenderUserId,
            State = conversation.State,
            Title = conversation.Title,
            Description = conversation.Description,
            LocationText = conversation.LocationText,
            Latitude = conversation.Latitude,
            Longitude = conversation.Longitude,
            AreaId = conversation.AreaId,
            AreaName = conversation.Area?.AreaName,
            FeedbackId = conversation.FeedbackId,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt
        };
    }

    private static bool TryGetFeedbackHistoryPage(string command, out int pageNumber)
    {
        pageNumber = 1;
        if (command is "VIEW FEEDBACKS" or "PHAN ANH DA GUI" or "XEM PHAN ANH")
        {
            return true;
        }

        if (command.StartsWith("TRANG ", StringComparison.Ordinal))
        {
            return int.TryParse(command[6..], out pageNumber) && pageNumber > 0;
        }

        const string prefix = "VIEW FEEDBACKS:";
        return command.StartsWith(prefix, StringComparison.Ordinal) &&
               int.TryParse(command[prefix.Length..], out pageNumber) &&
               pageNumber > 0;
    }

    private static string GetStatusLabel(string status)
    {
        return status switch
        {
            FeedbackStatus.Submitted => "Đã tiếp nhận",
            FeedbackStatus.AiReviewed => "Đã phân loại",
            FeedbackStatus.Verified => "Đã xác minh",
            FeedbackStatus.Assigned => "Đã phân công",
            FeedbackStatus.InProgress => "Đang xử lý",
            FeedbackStatus.Resolved => "Đã xử lý",
            FeedbackStatus.SubmittedForApproval => "Chờ duyệt",
            FeedbackStatus.Approved => "Đã duyệt",
            FeedbackStatus.Rejected => "Bị từ chối",
            FeedbackStatus.NeedRework => "Cần xử lý lại",
            FeedbackStatus.Closed => "Đã đóng",
            FeedbackStatus.Cancelled => "Đã hủy",
            _ => status
        };
    }

    private static IEnumerable<string> SplitMessage(string message, int maximumLength)
    {
        for (var offset = 0; offset < message.Length; offset += maximumLength)
        {
            var length = Math.Min(maximumLength, message.Length - offset);
            yield return message.Substring(offset, length);
        }
    }

    private static bool TryParseCoordinate(
        string? value,
        decimal minimum,
        decimal maximum,
        out decimal coordinate)
    {
        return decimal.TryParse(
                   value,
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out coordinate) &&
               coordinate >= minimum &&
               coordinate <= maximum;
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

        return builder.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('Đ', 'D')
            .Replace('_', ' ');
    }
}
