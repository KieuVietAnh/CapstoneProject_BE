using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private const string Idle = "Idle";
    private const string AwaitingTitle = "AwaitingTitle";
    private const string AwaitingDescription = "AwaitingDescription";
    private const string AwaitingLocation = "AwaitingLocation";
    private const string AwaitingArea = "AwaitingArea";
    private const string AwaitingConfirmation = "AwaitingConfirmation";
    private const string Submitting = "Submitting";
    private const string Completed = "Completed";
    private const int FeedbackHistoryPageSize = 5;
    private const int MaximumQuickReplies = 13;

    private static readonly MessengerQuickReplyOption CancelQuickReply =
        new("Hủy", "CANCEL");

    private static readonly IReadOnlyCollection<MessengerQuickReplyOption> ConfirmationQuickReplies =
    [
        new("Xác nhận", "XAC NHAN"),
        new("Nhập lại", "START_FEEDBACK"),
        CancelQuickReply
    ];

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

        var text = messagingEvent.Message?.QuickReply?.Payload ??
            messagingEvent.Message?.Text ??
            messagingEvent.Postback?.Payload;
        if (string.IsNullOrWhiteSpace(text))
        {
            await SendMainMenuAsync(
                senderPsid,
                "Hiện tại bot chỉ nhận nội dung chữ. Bạn muốn thực hiện thao tác nào?",
                cancellationToken);
            return;
        }

        if (conversation == null)
        {
            conversation = new MessengerFeedbackConversation
            {
                PageId = pageId,
                SenderPsid = senderPsid,
                State = Idle,
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
            await SendDraftPromptAsync(
                conversation.SenderPsid,
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
            await SendHelpAsync(conversation.SenderPsid, cancellationToken);
            return;
        }

        if (command is "MENU" or "MAIN_MENU")
        {
            SetIdle(conversation);
            await _uow.SaveAsync();
            await SendMainMenuAsync(
                conversation.SenderPsid,
                "Bạn muốn thực hiện thao tác nào?",
                cancellationToken);
            return;
        }

        if (command is "HUY" or "CANCEL")
        {
            SetIdle(conversation);
            await _uow.SaveAsync();
            await SendMainMenuAsync(
                conversation.SenderPsid,
                "Đã hủy nội dung đang nhập.",
                cancellationToken);
            return;
        }

        switch (conversation.State)
        {
            case Idle:
                await SendMainMenuAsync(
                    conversation.SenderPsid,
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
                await SendMainMenuAsync(
                    conversation.SenderPsid,
                    $"Phản ánh gần nhất đã được tạo với mã {conversation.FeedbackId}.",
                    cancellationToken);
                break;
            default:
                SetIdle(conversation);
                await _uow.SaveAsync();
                await SendMainMenuAsync(
                    conversation.SenderPsid,
                    "Hội thoại đã được đặt lại. Bạn muốn thực hiện thao tác nào?",
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
            await SendDraftPromptAsync(
                conversation.SenderPsid,
                "Tiêu đề tối đa 200 ký tự. Vui lòng nhập ngắn gọn hơn.",
                cancellationToken);
            return;
        }

        conversation.Title = text;
        conversation.State = AwaitingDescription;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
        await SendDraftPromptAsync(
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
            await SendDraftPromptAsync(
                conversation.SenderPsid,
                "Mô tả tối đa 4.000 ký tự. Vui lòng rút gọn nội dung.",
                cancellationToken);
            return;
        }

        conversation.Description = text;
        conversation.State = AwaitingLocation;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
        await SendDraftPromptAsync(
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
            await SendDraftPromptAsync(
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
        if (command is not ("XAC NHAN" or "DONG Y" or "YES" or "OK"))
        {
            await SendQuickRepliesAsync(
                conversation.SenderPsid,
                "Chọn Xác nhận để gửi phản ánh hoặc Nhập lại để bắt đầu lại.",
                ConfirmationQuickReplies,
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
            await SendDraftPromptAsync(
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
                GeoSource = "Messenger",
                SubmissionChannel = FeedbackSubmissionChannel.Messenger
            },
            []);

        await _uow.GetRepository<MessengerFeedbackSubmission>().AddAsync(
            new MessengerFeedbackSubmission
            {
                ConversationId = conversation.ConversationId,
                FeedbackId = feedback.FeedbackId,
                CreatedAt = DateTime.UtcNow
            });

        conversation.FeedbackId = feedback.FeedbackId;
        conversation.State = Completed;
        conversation.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        await SendMainMenuAsync(
            conversation.SenderPsid,
            $"Phản ánh đã được tiếp nhận thành công. Mã phản ánh: {feedback.FeedbackId}.",
            cancellationToken);
    }

    private async Task SendAreaChoicesAsync(string senderPsid, CancellationToken cancellationToken)
    {
        var areas = await GetActiveAreasAsync(cancellationToken);
        if (areas.Count == 0)
        {
            await SendDraftPromptAsync(
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

        var choices = areas
            .Take(MaximumQuickReplies - 1)
            .Select(area => new MessengerQuickReplyOption(
                FitQuickReplyTitle(area.AreaName),
                area.AreaId.ToString(CultureInfo.InvariantCulture)))
            .Append(CancelQuickReply)
            .ToList();

        await SendQuickRepliesAsync(
            senderPsid,
            $"Chọn khu vực bằng cách nhập mã số hoặc tên:\n{string.Join("\n", lines)}{suffix}",
            choices,
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
                      "Chọn Xác nhận để gửi hoặc Nhập lại để bắt đầu lại.";
        await SendQuickRepliesAsync(
            conversation.SenderPsid,
            summary,
            ConfirmationQuickReplies,
            cancellationToken);
    }

    private Task SendDraftPromptAsync(
        string senderPsid,
        string text,
        CancellationToken cancellationToken)
    {
        return SendQuickRepliesAsync(
            senderPsid,
            text,
            [CancelQuickReply],
            cancellationToken);
    }

    private Task SendMainMenuAsync(
        string senderPsid,
        string text,
        CancellationToken cancellationToken)
    {
        return SendQuickRepliesAsync(
            senderPsid,
            text,
            [
                new MessengerQuickReplyOption("Gửi phản ánh", "START_FEEDBACK"),
                new MessengerQuickReplyOption("Phản ánh đã gửi", "VIEW_FEEDBACKS:1"),
                new MessengerQuickReplyOption("Trợ giúp", "HELP")
            ],
            cancellationToken);
    }

    private Task SendHelpAsync(string senderPsid, CancellationToken cancellationToken)
    {
        const string helpText =
            "Bạn có thể gửi phản ánh mới hoặc xem lại các phản ánh đã gửi từ Messenger. " +
            "Khi tạo phản ánh, bot sẽ lần lượt hỏi tiêu đề, mô tả, vị trí, khu vực và yêu cầu xác nhận.";
        return SendMainMenuAsync(senderPsid, helpText, cancellationToken);
    }

    private async Task SendFeedbackHistoryAsync(
        MessengerFeedbackConversation conversation,
        int requestedPage,
        CancellationToken cancellationToken)
    {
        var submissions = _uow.GetRepository<MessengerFeedbackSubmission>().Entities
            .AsNoTracking()
            .Where(submission => submission.ConversationId == conversation.ConversationId);

        var totalItems = await submissions.CountAsync(cancellationToken);
        if (totalItems == 0)
        {
            await SendMainMenuAsync(
                conversation.SenderPsid,
                "Bạn chưa gửi phản ánh nào từ Messenger.",
                cancellationToken);
            return;
        }

        var totalPages = (int)Math.Ceiling(totalItems / (double)FeedbackHistoryPageSize);
        var pageNumber = Math.Clamp(requestedPage, 1, totalPages);
        var items = await submissions
            .OrderByDescending(submission => submission.CreatedAt)
            .Skip((pageNumber - 1) * FeedbackHistoryPageSize)
            .Take(FeedbackHistoryPageSize)
            .Select(submission => new
            {
                submission.Feedback.FeedbackId,
                submission.Feedback.Title,
                submission.Feedback.Status,
                submission.Feedback.CreatedAt
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

        var choices = new List<MessengerQuickReplyOption>();
        if (pageNumber > 1)
        {
            choices.Add(new MessengerQuickReplyOption("Trang trước", $"VIEW_FEEDBACKS:{pageNumber - 1}"));
        }

        if (pageNumber < totalPages)
        {
            choices.Add(new MessengerQuickReplyOption("Trang sau", $"VIEW_FEEDBACKS:{pageNumber + 1}"));
        }

        choices.Add(new MessengerQuickReplyOption("Menu", "MENU"));
        await SendQuickRepliesAsync(
            conversation.SenderPsid,
            message.ToString().TrimEnd(),
            choices,
            cancellationToken);
    }

    private static bool TryGetFeedbackHistoryPage(string command, out int pageNumber)
    {
        pageNumber = 1;
        if (command is "VIEW_FEEDBACKS" or "PHAN ANH DA GUI" or "XEM PHAN ANH")
        {
            return true;
        }

        const string prefix = "VIEW_FEEDBACKS:";
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

    private Task SendTextAsync(
        string senderPsid,
        string text,
        CancellationToken cancellationToken)
    {
        return SendMessageAsync(senderPsid, text, [], cancellationToken);
    }

    private Task SendQuickRepliesAsync(
        string senderPsid,
        string text,
        IReadOnlyCollection<MessengerQuickReplyOption> quickReplies,
        CancellationToken cancellationToken)
    {
        return SendMessageAsync(senderPsid, text, quickReplies, cancellationToken);
    }

    private async Task SendMessageAsync(
        string senderPsid,
        string text,
        IReadOnlyCollection<MessengerQuickReplyOption> quickReplies,
        CancellationToken cancellationToken)
    {
        var accessToken = GetRequiredConfiguration("Messenger:PageAccessToken");
        var graphVersion = _configuration["Messenger:GraphApiVersion"] ?? "v25.0";
        var endpoint = $"https://graph.facebook.com/{graphVersion}/me/messages";
        var chunks = SplitMessage(text, 1800).ToList();
        var outgoingQuickReplies = quickReplies
            .Take(MaximumQuickReplies)
            .Select(option => new OutgoingMessengerQuickReply
            {
                Title = FitQuickReplyTitle(option.Title),
                Payload = option.Payload
            })
            .ToList();

        for (var index = 0; index < chunks.Count; index++)
        {
            var isLastChunk = index == chunks.Count - 1;
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new
                {
                    recipient = new { id = senderPsid },
                    messaging_type = "RESPONSE",
                    message = new OutgoingMessengerMessage
                    {
                        Text = chunks[index],
                        QuickReplies = isLastChunk && outgoingQuickReplies.Count > 0
                            ? outgoingQuickReplies
                            : null
                    }
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

    private static string FitQuickReplyTitle(string title)
    {
        var trimmed = title.Trim();
        return trimmed.Length <= 20 ? trimmed : $"{trimmed[..17]}...";
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
                        user.Role.RoleName.ToUpper() == UserRole.SERVICEUSER,
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

    private static void SetIdle(MessengerFeedbackConversation conversation)
    {
        ResetDraft(conversation);
        conversation.State = Idle;
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

    private sealed record MessengerQuickReplyOption(string Title, string Payload);

    private sealed class OutgoingMessengerMessage
    {
        [JsonPropertyName("text")]
        public string Text { get; init; } = null!;

        [JsonPropertyName("quick_replies")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyCollection<OutgoingMessengerQuickReply>? QuickReplies { get; init; }
    }

    private sealed class OutgoingMessengerQuickReply
    {
        [JsonPropertyName("content_type")]
        public string ContentType { get; init; } = "text";

        [JsonPropertyName("title")]
        public string Title { get; init; } = null!;

        [JsonPropertyName("payload")]
        public string Payload { get; init; } = null!;
    }
}
